using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Topos.Hypergraph;

/// <summary>
/// M0 storage kernel: Handle allocation, the vertex table, incidence storage, and typed property
/// pools — the four primitives and two invariants of spec §3.
///
/// Concurrency model (spec §3.4, Single-Writer/Multi-Reader at the kernel boundary):
/// <list type="bullet">
/// <item>Handle allocation is genuinely lock-free (<see cref="HandleAllocator"/>,
/// <c>Interlocked.Increment</c>).</item>
/// <item>Incidence indexes are genuinely lock-free for readers: each update copies-on-write into
/// a new <see cref="ImmutableArray{T}"/> via <see cref="ConcurrentDictionary{TKey,TValue}"/>, so
/// a reader always sees a complete, consistent snapshot without taking a lock.</item>
/// <item>The vertex table and every property pool are each one <see cref="PropertyPool{T}"/> —
/// a <see cref="SparseSet{T}"/> behind its own <see cref="ReaderWriterLockSlim"/>. This is the
/// "per-pool, not global" granularity: concurrent access to different pools never contends.</item>
/// </list>
///
/// <b>Write methods assume a single-writer thread</b>, per the SWMR model — concurrent calls to
/// write methods (<see cref="CreateVertex"/>, <see cref="SetDormant"/>, <see cref="SetProperty{T}"/>,
/// etc.) from multiple threads are not a supported configuration without external synchronization
/// (the incidence-index writes are the one exception: they're safe under concurrent writers too,
/// since <see cref="ConcurrentDictionary{TKey,TValue}.AddOrUpdate(TKey,System.Func{TKey,TValue},System.Func{TKey,TValue,TValue})"/>
/// is inherently CAS-safe). Read methods are always safe to call concurrently with the single
/// writer.
/// </summary>
public sealed class HypergraphKernel
{
    private readonly HandleAllocator _allocator = new();
    private readonly PropertyPool<Vertex> _vertices = new();
    private readonly ConcurrentDictionary<Handle, ImmutableArray<Incidence>> _bySource = new();
    private readonly ConcurrentDictionary<Handle, ImmutableArray<Incidence>> _byMember = new();
    private readonly PropertyRegistry _properties = new();
    private readonly ConcurrentDictionary<int, object> _propertyPools = new();

    // ── Vertices ─────────────────────────────────────────────────────────────

    public Handle CreateVertex(VertexRoles roles = VertexRoles.None)
    {
        var handle = _allocator.Next();
        _vertices.Set(handle, new Vertex(handle, roles, VertexStatus.Active));
        return handle;
    }

    public bool TryGetVertex(Handle handle, out Vertex vertex) => _vertices.TryGet(handle, out vertex);

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
        Append(_bySource, source, incidence);
        Append(_byMember, member, incidence);
        return incidence;
    }

    public ImmutableArray<Incidence> IncidencesFrom(Handle source) =>
        _bySource.TryGetValue(source, out var list) ? list : ImmutableArray<Incidence>.Empty;

    public ImmutableArray<Incidence> IncidencesOf(Handle member) =>
        _byMember.TryGetValue(member, out var list) ? list : ImmutableArray<Incidence>.Empty;

    private static void Append(
        ConcurrentDictionary<Handle, ImmutableArray<Incidence>> index, Handle key, Incidence incidence)
    {
        index.AddOrUpdate(
            key,
            addValueFactory: static (_, inc) => ImmutableArray.Create(inc),
            updateValueFactory: static (_, existing, inc) => existing.Add(inc),
            factoryArgument: incidence);
    }

    // ── Properties ───────────────────────────────────────────────────────────

    public PropertyKey<T> ResolveProperty<T>(string name) => _properties.Resolve<T>(name);

    public void SetProperty<T>(PropertyKey<T> key, Handle handle, T value) =>
        GetPool<T>(key).Set(handle, value);

    public bool TryGetProperty<T>(PropertyKey<T> key, Handle handle, out T value) =>
        GetPool<T>(key).TryGet(handle, out value);

    public bool RemoveProperty<T>(PropertyKey<T> key, Handle handle) =>
        GetPool<T>(key).Remove(handle);

    private PropertyPool<T> GetPool<T>(PropertyKey<T> key) =>
        (PropertyPool<T>)_propertyPools.GetOrAdd(key.Id, static _ => new PropertyPool<T>());
}
