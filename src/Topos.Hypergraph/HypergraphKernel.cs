using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Topos.Hypergraph;

/// <summary>
/// M0 storage kernel: Handle allocation, the vertex table, incidence storage, and typed property
/// pools — the four primitives and two invariants of spec §3.
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

    public HypergraphKernel() => _allocator = new HandleAllocator();

    /// <summary>M4 persistence (spec §6): resumes Handle allocation after a reload rather than from 0, so a newly created vertex can never collide with one restored from a snapshot. See <see cref="HandleAllocator"/>'s constructor doc.</summary>
    public HypergraphKernel(uint startingHandleIndex) => _allocator = new HandleAllocator(startingHandleIndex);

    /// <summary>The Index the next <see cref="CreateVertex"/> call will allocate — the snapshot-side counterpart consumers persist alongside vertex/incidence data (spec §6 M4).</summary>
    public uint NextHandleIndex => _allocator.NextIndex;

    // ── Vertices ─────────────────────────────────────────────────────────────

    public Handle CreateVertex(VertexRoles roles = VertexRoles.None)
    {
        var handle = _allocator.Next();
        _vertices.Set(handle, new Vertex(handle, roles, VertexStatus.Active));
        return handle;
    }

    public bool TryGetVertex(Handle handle, out Vertex vertex) => _vertices.TryGet(handle, out vertex);

    /// <summary>
    /// Inserts a vertex at a specific, already-allocated Handle — bypassing
    /// <see cref="_allocator"/> entirely. For snapshot reload only (spec §6 M4): normal code
    /// should always go through <see cref="CreateVertex"/>, which is the only path that keeps
    /// the allocator's monotonic-never-reused guarantee (Invariant 1) intact. Calling this with a
    /// Handle the allocator doesn't yet know about (e.g. Index &gt;= <see cref="NextHandleIndex"/>)
    /// creates exactly the collision Invariant 1 forbids — the caller (here, only
    /// <c>HypergraphSnapshot.Load</c>) is responsible for restoring the allocator's cursor first.
    /// </summary>
    public void RestoreVertex(Handle handle, VertexRoles roles, VertexStatus status) =>
        _vertices.Set(handle, new Vertex(handle, roles, status));

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

    public void Reactivate(Handle handle) => UpdateStatus(handle, VertexStatus.Active);

    private void UpdateStatus(Handle handle, VertexStatus status)
    {
        if (_vertices.TryGet(handle, out var v) && v.Status != status)
            _vertices.Set(handle, v with { Status = status });
    }

    // ── Incidences ───────────────────────────────────────────────────────────

    /// <summary>
    /// Records one membership. No existence check beyond having been allocated at some point —
    /// provenance edges always resolve, even to dormant targets (Invariant 1).
    /// </summary>
    public Incidence AddIncidence(Handle source, Handle member, byte role, int ordinal)
    {
        var incidence = new Incidence(source, member, role, ordinal);
        _bySource.Append(source, incidence);
        _byMember.Append(member, incidence);
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

    public PropertyKey<T> ResolveProperty<T>(string name) => _properties.Resolve<T>(name);

    public void SetProperty<T>(PropertyKey<T> key, Handle handle, T value) =>
        GetPool<T>(key).Set(handle, value);

    public bool TryGetProperty<T>(PropertyKey<T> key, Handle handle, out T value) =>
        GetPool<T>(key).TryGet(handle, out value);

    public bool RemoveProperty<T>(PropertyKey<T> key, Handle handle) =>
        GetPool<T>(key).Remove(handle);

    /// <summary>Every (Handle, Value) pair currently set for <paramref name="key"/> — the columnar dump M4 snapshot persistence writes out per property (spec §6).</summary>
    public ImmutableArray<(Handle Handle, T Value)> EnumerateProperty<T>(PropertyKey<T> key) =>
        GetPool<T>(key).Entries;

    private PropertyPool<T> GetPool<T>(PropertyKey<T> key) =>
        (PropertyPool<T>)_propertyPools.GetOrAdd(key.Id, static _ => new PropertyPool<T>());
}
