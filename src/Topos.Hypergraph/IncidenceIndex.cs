using System.Collections.Immutable;

namespace Topos.Hypergraph;

/// <summary>
/// A key→many-Incidences index (backs <c>HypergraphKernel</c>'s source and member directions).
///
/// <b>Replaces an earlier copy-on-write design</b> (<c>ConcurrentDictionary&lt;Handle,
/// ImmutableArray&lt;Incidence&gt;&gt;</c>) that measured badly: O(N²) total cost for N appends
/// into the same key, because every append copied the whole array — see
/// <c>docs/M0_BENCHMARK_RESULTS_2026-07-24.md</c> §3 (55ms to build one 8,000-member hub vertex,
/// against a 3.7ms total step budget). This version stores each key's incidences as a plain
/// <see cref="List{Incidence}"/> — amortized O(1) append — inside the same
/// <see cref="SparseSet{T}"/> + <see cref="ReaderWriterLockSlim"/> pattern <c>PropertyPool{T}</c>
/// already uses, which benchmarked ~2.2–2.4× faster than a naive Dictionary (§1 of the same
/// report). One structural pattern for every pool in the kernel now, not two.
///
/// <see cref="Get"/> takes an O(k) snapshot copy per *read* (k = current member count) instead of
/// per *write* — the right trade for a write-heavy accumulator, and it preserves the invariant
/// that a returned <see cref="ImmutableArray{T}"/> is safe to use after the lock is released
/// (a live view over the mutable <see cref="List{T}"/> would not be).
/// </summary>
internal sealed class IncidenceIndex
{
    private readonly SparseSet<List<Incidence>> _byKey = new();
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);

    public void Append(Handle key, Incidence incidence)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_byKey.TryGet(key, out var list))
            {
                list = [];
                _byKey.Set(key, list);
            }
            list.Add(incidence);
        }
        finally { _lock.ExitWriteLock(); }
    }

    public ImmutableArray<Incidence> Get(Handle key)
    {
        _lock.EnterReadLock();
        try
        {
            return _byKey.TryGet(key, out var list)
                ? ImmutableArray.CreateRange(list)
                : ImmutableArray<Incidence>.Empty;
        }
        finally { _lock.ExitReadLock(); }
    }
}
