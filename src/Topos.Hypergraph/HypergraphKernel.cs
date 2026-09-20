using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Topos.Hypergraph;

/// <summary>
/// M0 storage kernel: Handle allocation, the vertex table, incidence storage, and typed property
/// pools — the four primitives and three invariants of spec §3.
///
/// Concurrency model (spec §3.4, Single-Writer/Multi-Reader at the kernel boundary) — revised
/// 2026-07-24 after benchmark data, see <c>docs/M0_BENCHMARK_RESULTS_2026-07-24.md</c>:
/// <list type="bullet">
/// <item>Handle allocation is genuinely lock-free (<see cref="HandleAllocator"/>,
/// <c>Interlocked.Increment</c>).</item>
/// <item><b>Every other piece of state — the vertex table, the incidence indexes, and every
/// property pool — is one <see cref="SparseSet{T}"/> behind its own
/// <see cref="ReaderWriterLockSlim"/></b> (<see cref="PropertyPool{T}"/> for vertices/properties,
/// <see cref="IncidenceIndex"/> for incidences). This is a change from the original design, which
/// used copy-on-write <see cref="ImmutableArray{T}"/> for incidences specifically, chasing
/// lock-free reads. Measured cost of that choice: 5–6× slower than a naive
/// <c>Dictionary&lt;Handle,List&lt;Handle&gt;&gt;</c> even in the benign case, and O(N²) —
/// literally thousands of times slower — for a hub vertex accumulating many members. RWLS reads
/// are cheap enough uncontended that the lock-free property wasn't worth what it cost. One
/// concurrency pattern for the whole kernel now, not two.</item>
/// </list>
///
/// <b>Write methods assume a single-writer thread</b>, per the SWMR model — concurrent calls to
/// write methods (<see cref="CreateVertex"/>, <see cref="SetDormant"/>, <see cref="SetProperty{T}"/>,
/// etc.) from multiple threads are not a supported configuration without external synchronization.
/// Read methods are always safe to call concurrently with the single writer.
/// </summary>
public sealed class HypergraphKernel : IHypergraphQuery
{
    private readonly HandleAllocator _allocator;
    private readonly PropertyPool<Vertex> _vertices = new();
    private readonly IncidenceIndex _bySource = new();
    private readonly IncidenceIndex _byMember = new();
    private readonly PropertyRegistry _properties = new();
    private readonly ConcurrentDictionary<int, object> _propertyPools = new();

    private long _revision;
    private ChangeLog? _changeLog;

    public HypergraphKernel() => _allocator = new HandleAllocator();

    /// <summary>M4 persistence (spec §6): resumes Handle allocation after a reload rather than from 0, so a newly created vertex can never collide with one restored from a snapshot. See <see cref="HandleAllocator"/>'s constructor doc.</summary>
    public HypergraphKernel(uint startingHandleIndex) => _allocator = new HandleAllocator(startingHandleIndex);

    /// <summary>The Index the next <see cref="CreateVertex"/> call will allocate — the snapshot-side counterpart consumers persist alongside vertex/incidence data (spec §6 M4).</summary>
    public uint NextHandleIndex => _allocator.NextIndex;

    // ── Vertices ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Allocates a new vertex with a fresh, never-reused <see cref="Handle"/> and
    /// <see cref="VertexStatus.Active"/> status. This is the only path that keeps the allocator's
    /// monotonic-never-reused guarantee (Invariant 1) intact — see <see cref="RestoreVertex"/> for
    /// the one sanctioned exception (snapshot reload).
    /// </summary>
    public Handle CreateVertex(VertexRoles roles = VertexRoles.None)
    {
        var handle = _allocator.Next();
        _vertices.Set(handle, new Vertex(handle, roles, VertexStatus.Active));
        RecordChange(handle, ChangeKind.VertexCreated);
        return handle;
    }

    public bool TryGetVertex(Handle handle, out Vertex vertex)
    {
        if (_vertices.TryGet(handle, out vertex)) return true;
        vertex = new Vertex(Handle.Invalid, VertexRoles.None, VertexStatus.Dormant);
        return false;
    }

    /// <summary>
    /// Inserts a vertex at a specific, already-allocated Handle — bypassing
    /// <see cref="_allocator"/> entirely. For snapshot reload only (spec §6 M4): normal code
    /// should always go through <see cref="CreateVertex"/>, which is the only path that keeps
    /// the allocator's monotonic-never-reused guarantee (Invariant 1) intact. Calling this with a
    /// Handle the allocator doesn't yet know about (e.g. Index &gt;= <see cref="NextHandleIndex"/>)
    /// creates exactly the collision Invariant 1 forbids — the caller (here, only
    /// <c>HypergraphSnapshot.Load</c>) is responsible for restoring the allocator's cursor first.
    /// </summary>
    /// <remarks>
    /// <b>Does not move <see cref="CurrentRevision"/> and records nothing in the change log</b>
    /// (M13, `docs/LIVING_GRAPH_SPEC.md` §6.6). A reload is a new baseline, not a change: emitting
    /// one "change" per restored vertex would overflow any bounded log on the first snapshot load
    /// and would tell a consumer that a hundred thousand things moved when nothing did. A reloaded
    /// kernel starts at revision 0 with an empty log, so any consumer holding a pre-reload cursor
    /// gets an overflow signal and rescans, which is the correct outcome.
    /// </remarks>
    public void RestoreVertex(Handle handle, VertexRoles roles, VertexStatus status) =>
        _vertices.Set(handle, new Vertex(handle, roles, status));

    /// <summary>
    /// Inserts an incidence at replay time — the incidence counterpart to
    /// <see cref="RestoreVertex"/>, for snapshot reload only (spec §6 M4 + §6.6 M13).
    /// <b>Does not move <see cref="CurrentRevision"/> and records nothing in the change log</b>
    /// (same rationale: a reload is a new baseline, not a change). Replay via
    /// <see cref="AddIncidence"/> would advance the revision once per incidence, leaving a
    /// reloaded kernel claiming <c>CurrentRevision == incidenceCount</c> with an empty log —
    /// exactly the silent staleness the M13 overflow contract exists to prevent.
    /// </summary>
    public Incidence RestoreIncidence(Handle source, Handle member, byte role, int ordinal)
    {
        var incidence = new Incidence(source, member, role, ordinal);
        _bySource.Append(source, incidence);
        _byMember.Append(member, incidence);
        return incidence;
    }

    /// <summary>
    /// Sets a property without moving <see cref="CurrentRevision"/> and without recording in the
    /// change log — the property counterpart to <see cref="RestoreVertex"/>, for snapshot reload
    /// only (property columns in <c>HypergraphSnapshot.Load</c>).
    /// </summary>
    public void RestoreProperty<T>(PropertyKey<T> key, Handle handle, T value) =>
        GetPool<T>(key).Set(handle, value);

    // ── IHypergraphQuery primitives ─────────────────────────────────────────

    public int CountVertices() => _vertices.Count;

    public IReadOnlyList<Handle> VertexHandles() => _vertices.Handles;

    public IReadOnlyList<Handle> GetVertexHyperedges(Handle vertex) =>
        [.. IncidencesOf(vertex).Select(i => i.Source).Distinct()];

    public IReadOnlyList<Incidence> GetHyperedgeVertices(Handle hyperedge) =>
        IncidencesFrom(hyperedge);

    /// <summary>
    /// Tombstones a vertex (Invariant 1: dormant, never removed — this never calls
    /// <see cref="SparseSet{T}.Remove"/>; the vertex stays resolvable via
    /// <see cref="TryGetVertex"/> forever).
    /// </summary>
    public void SetDormant(Handle handle) => UpdateStatus(handle, VertexStatus.Dormant);

    /// <summary>
    /// Reverses <see cref="SetDormant"/> — sets a vertex back to <see cref="VertexStatus.Active"/>.
    /// A no-op if <paramref name="handle"/> is unknown or already active.
    /// </summary>
    public void Reactivate(Handle handle) => UpdateStatus(handle, VertexStatus.Active);

    private void UpdateStatus(Handle handle, VertexStatus status)
    {
        if (_vertices.TryGet(handle, out var v) && v.Status != status)
        {
            _vertices.Set(handle, v with { Status = status });
            RecordChange(handle, ChangeKind.StatusChanged);
        }
    }

    // ── Incidences ───────────────────────────────────────────────────────────

    /// <summary>
    /// Records one membership. No existence check beyond having been allocated at some point —
    /// provenance edges always resolve, even to dormant targets (Invariant 1).
    ///
    /// <b>This method has no counterpart, deliberately — Invariant 3 (documented 2026-09-16,
    /// <c>docs/DECISIONS.md</c>; behavior unchanged since M0).</b> A membership, once recorded, is
    /// permanent: a relation can gain members, never lose one. The line the kernel draws is that
    /// <i>properties are mutable, structure is append-only</i> — <see cref="SetProperty{T}"/>
    /// overwrites and <see cref="RemoveProperty{T}"/> deletes, because a property is a claim about
    /// the present, while an <see cref="Incidence"/> is a claim that a relation <i>was asserted</i>,
    /// and revising the present must not silently rewrite that. It is the structural half of
    /// Invariant 1: that one makes a <see cref="Handle"/> safe to hold for a long time, this one
    /// makes an <see cref="Incidence"/> safe to hold.
    ///
    /// To revise a relation, supersede it rather than editing it — add the corrected hyperedge, then
    /// <see cref="SetDormant"/> the old one. Both stay resolvable, so the revision is itself part of
    /// the record, which is what provenance chains and <c>Topos.Hypergraph.Living</c>'s observation
    /// chains are built on. See <c>docs/USAGE_PATTERNS.md</c> Pattern 11.
    /// </summary>
    public Incidence AddIncidence(Handle source, Handle member, byte role, int ordinal)
    {
        var incidence = new Incidence(source, member, role, ordinal);
        _bySource.Append(source, incidence);
        _byMember.Append(member, incidence);
        RecordChange(source, member, ChangeKind.IncidenceAdded);
        return incidence;
    }

    /// <summary>Outgoing direction: every Incidence where <paramref name="source"/> is the edge/source side.</summary>
    public ImmutableArray<Incidence> IncidencesFrom(Handle source) => _bySource.Get(source);

    /// <summary>Reverse direction: every Incidence where <paramref name="member"/> participates as a member — the index that makes "which hyperedges is this vertex part of" O(1) instead of a full scan.</summary>
    public ImmutableArray<Incidence> IncidencesOf(Handle member) => _byMember.Get(member);

    /// <summary>
    /// Every Incidence in the kernel, exactly once each — O(V) by iterating every vertex's
    /// outgoing incidences, since every Incidence has exactly one Source. No dedicated "all
    /// incidences" index exists (nothing needed one until M4 snapshot persistence, spec §6);
    /// added for that consumer rather than speculatively.
    /// </summary>
    public IEnumerable<Incidence> AllIncidences() => VertexHandles().SelectMany(h => IncidencesFrom(h));

    // ── Properties ───────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the stable <see cref="PropertyKey{T}"/> for <paramref name="name"/>, assigning a
    /// registry slot on first use (<see cref="PropertyRegistry"/>). Call once and cache the
    /// result — repeated resolution is a dictionary lookup, not free, and every
    /// <see cref="SetProperty{T}"/>/<see cref="TryGetProperty{T}"/>/<see cref="RemoveProperty{T}"/>
    /// call needs the same <typeparamref name="T"/> for a given <paramref name="name"/> (see
    /// <see cref="PropertyRegistry"/>'s doc for what happens if you don't).
    /// </summary>
    public PropertyKey<T> ResolveProperty<T>(string name) => _properties.Resolve<T>(name);

    /// <summary>Sets <paramref name="key"/>'s value for <paramref name="handle"/>, creating the property pool on first use. No existence check on <paramref name="handle"/> — properties on a not-yet-created or dormant Handle are legal (provenance edges always resolve, Invariant 1).</summary>
    public void SetProperty<T>(PropertyKey<T> key, Handle handle, T value)
    {
        GetPool<T>(key).Set(handle, value);
        RecordChange(handle, ChangeKind.PropertySet);
    }

    /// <summary>Resolves <paramref name="value"/> for <paramref name="key"/> on <paramref name="handle"/>. Returns <c>false</c> — with <paramref name="value"/> left as <c>default(T)</c> — if no value was ever set for this (key, handle) pair.</summary>
    public bool TryGetProperty<T>(PropertyKey<T> key, Handle handle, out T value) =>
        GetPool<T>(key).TryGet(handle, out value);

    /// <summary>Removes <paramref name="key"/>'s value for <paramref name="handle"/>, if any. Returns whether a value was actually present to remove.</summary>
    public bool RemoveProperty<T>(PropertyKey<T> key, Handle handle)
    {
        if (!GetPool<T>(key).Remove(handle)) return false;
        RecordChange(handle, ChangeKind.PropertyRemoved);
        return true;
    }

    /// <summary>Every (Handle, Value) pair currently set for <paramref name="key"/> — the columnar dump M4 snapshot persistence writes out per property (spec §6).</summary>
    public ImmutableArray<(Handle Handle, T Value)> EnumerateProperty<T>(PropertyKey<T> key) =>
        GetPool<T>(key).Entries;

    private PropertyPool<T> GetPool<T>(PropertyKey<T> key) =>
        (PropertyPool<T>)_propertyPools.GetOrAdd(key.Id, static _ => new PropertyPool<T>());

    // ── Change tracking (M13, docs/LIVING_GRAPH_SPEC.md) ─────────────────────

    /// <summary>
    /// Monotonic count of writes applied to this kernel. Starts at 0 and increases by one per
    /// write that actually changed something. Two reads returning the same value mean nothing
    /// changed in between — which turns "is my view stale?" from an O(V) rescan into an integer
    /// compare, and is the whole reason this is always on rather than opt-in like the change log
    /// (`docs/LIVING_GRAPH_SPEC.md` §6.1): one increment per write is genuinely free, while
    /// retaining *what* changed is not.
    ///
    /// <b>May over-report, never under-reports.</b> <see cref="SetProperty{T}"/> counts as a write
    /// even when it stores a value equal to the one already there, because detecting that would
    /// cost an equality comparison on every property write to spare consumers an occasional
    /// redundant re-read. Writes that the kernel can see are no-ops for free do not count:
    /// <see cref="SetDormant"/>/<see cref="Reactivate"/> on a vertex already in that status, and
    /// <see cref="RemoveProperty{T}"/> with nothing to remove, both leave this untouched.
    /// <see cref="RestoreVertex"/> never counts — see its own remarks.
    ///
    /// Safe to read from any thread while the single writer runs.
    /// </summary>
    public long CurrentRevision => Volatile.Read(ref _revision);

    /// <summary>Whether <see cref="ChangesSince"/> can be called — see <see cref="EnableChangeLog"/>.</summary>
    public bool IsChangeLogEnabled => _changeLog is not null;

    /// <summary>
    /// Starts retaining <i>what</i> changed, not merely that something did, in a bounded ring of
    /// <paramref name="capacity"/> entries. <b>Off by default</b> (`docs/LIVING_GRAPH_SPEC.md`
    /// §6.1): every write pays for the log, and most consumers today never read it, so the cost is
    /// opt-in while <see cref="CurrentRevision"/> stays free for everyone.
    ///
    /// <paramref name="capacity"/> is measured in <b>distinct handles touched</b>, not in writes,
    /// because entries coalesce per handle — a single vertex written a million times occupies one
    /// entry. Size it against how many different things change between a consumer's polls, then;
    /// a consumer that falls further behind than that is told so rather than silently shorted.
    ///
    /// Calling this again replaces the log and discards its contents; existing consumer cursors
    /// will read as overflowed, which is the honest answer.
    /// </summary>
    public void EnableChangeLog(int capacity = 4096)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _changeLog = new ChangeLog(capacity);
    }

    /// <summary>Stops retaining changes and releases the ring. <see cref="CurrentRevision"/> keeps moving.</summary>
    public void DisableChangeLog() => _changeLog = null;

    /// <summary>
    /// Every handle touched since <paramref name="revision"/>, oldest first, each carrying every
    /// <see cref="ChangeKind"/> that has touched it since — the answer to "what do I need to
    /// re-read." Empty when nothing has changed.
    ///
    /// <paramref name="overflowed"/> is <c>true</c> when the ring has discarded a change this
    /// caller had not yet seen: the cursor is stale, the returned list is a suffix rather than the
    /// whole story, and the correct response is a full rescan. It is an <c>out</c> parameter rather
    /// than a property precisely so it cannot be ignored by accident — a change feed that drops
    /// changes quietly is worse than no feed, because consumers trust it.
    ///
    /// Pass the <see cref="CurrentRevision"/> you previously read; the returned entries are those
    /// strictly newer. Reading <see cref="CurrentRevision"/> after this call gives the cursor for
    /// next time.
    ///
    /// <b>An entry's <see cref="GraphChange.Kind"/> accumulates over that entry's whole life in the
    /// ring, not over the window since your cursor.</b> One ring serves every reader, so the kinds
    /// cannot be per-cursor. A handle created and later made dormant reports both to a reader who
    /// already saw the creation. This is the same over-report <see cref="CurrentRevision"/>
    /// documents: it can cost a consumer a redundant re-read, never a missed change. Test a kind
    /// with <see cref="Enum.HasFlag"/> rather than equality.
    /// </summary>
    /// <exception cref="InvalidOperationException">The change log was never enabled.</exception>
    public IReadOnlyList<GraphChange> ChangesSince(long revision, out bool overflowed)
    {
        var log = _changeLog ?? throw new InvalidOperationException(
            "The change log is off. Call EnableChangeLog() before ChangesSince() — CurrentRevision " +
            "works without it, but retaining what changed is opt-in (docs/LIVING_GRAPH_SPEC.md §6.1).");
        // A cursor AHEAD of CurrentRevision means the consumer's reality is gone — a pre-reload
        // cursor against a kernel that just restarted at revision 0 (M13 contract). Signal
        // overflow so it rescans instead of silently reading an empty change list.
        if (revision > CurrentRevision)
        {
            overflowed = true;
            return [];
        }
        return log.Since(revision, out overflowed);
    }

    /// <summary>
    /// Assigns the next revision, records it, and only then publishes it.
    ///
    /// <b>The ordering is the load-bearing part.</b> The log is written before
    /// <see cref="_revision"/> becomes visible, so a reader that observes revision N is guaranteed
    /// the log already contains N. Publishing first would open a window where a consumer reads a
    /// revision, calls <see cref="ChangesSince"/>, gets nothing back, and concludes — wrongly, and
    /// silently — that it is up to date. Reading <see cref="_revision"/> here needs no interlocked
    /// operation: under the SWMR model this runs on the single writer thread, which is the only
    /// thread that writes it.
    /// </summary>
    private void RecordChange(Handle handle, ChangeKind kind)
    {
        long next = _revision + 1;
        _changeLog?.Record(handle, kind, next);
        Volatile.Write(ref _revision, next);
    }

    /// <summary>
    /// Two handles, one write, one revision — <see cref="AddIncidence"/>, where the edge-vertex and
    /// the member are both affected by the same single change.
    /// </summary>
    private void RecordChange(Handle first, Handle second, ChangeKind kind)
    {
        long next = _revision + 1;
        if (_changeLog is { } log)
        {
            log.Record(first, kind, next);
            if (second != first) log.Record(second, kind, next);
        }
        Volatile.Write(ref _revision, next);
    }
}
