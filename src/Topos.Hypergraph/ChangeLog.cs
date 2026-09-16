namespace Topos.Hypergraph;

/// <summary>
/// The bounded, per-handle-coalescing change log behind <see cref="HypergraphKernel.ChangesSince"/>
/// (M13, `docs/LIVING_GRAPH_SPEC.md` §5.2).
///
/// <b>Deliberately the boring design.</b> A fixed-size ring behind one
/// <see cref="ReaderWriterLockSlim"/> — the same pattern <see cref="PropertyPool{T}"/> and
/// <see cref="IncidenceIndex"/> already use, and the same pattern that won the M0 benchmark after a
/// clever copy-on-write design lost to it by 5–6× in the benign case and thousands of times over on
/// a hub vertex (`docs/M0_BENCHMARK_RESULTS_2026-07-24.md` §2-3, §5). The instinct here is a
/// lock-free ring; the precedent says earn it with a measurement first, which
/// <c>ChangeLogBenchmarks</c> now supplies.
///
/// <b>Coalescing, and what it costs.</b> Each handle occupies exactly one slot, <b>updated in
/// place</b>: a write to a handle already in the ring rewrites its slot with the newer revision and
/// the union of the old and new <see cref="ChangeKind"/>s, and does <i>not</i> consume a new slot.
/// That is what makes the capacity a budget of <i>distinct handles touched</i> rather than of write
/// volume — a hot vertex written a million times costs one slot and evicts nothing.
///
/// The first version of this class got that wrong in a way worth recording: it retired the old slot
/// and appended a fresh one, so the write cursor advanced on every write and a single hot handle
/// still cycled the ring and evicted everything else. It passed every hand-written case except the
/// one that asserted the capacity claim directly — see
/// <c>ChangeLogTests.CapacityIsSpentOnDistinctHandles_NotOnWriteVolume</c>.
///
/// Updating in place costs the ordering: slots are no longer in revision order, so
/// <see cref="Since"/> sorts what it returns. That is O(k log k) in the number of changes returned,
/// paid on the rarer operation, which is the right side to pay on.
///
/// Eviction is by insertion order, not recency: when a genuinely new handle arrives at capacity,
/// the longest-resident slot goes, even if it was touched more recently than others. That is a
/// simplification, not a subtlety — it can only cause an honest overflow signal, never a silent
/// loss.
///
/// <b>Overflow is loud, never silent.</b> When the ring evicts a live slot, the highest evicted
/// revision is remembered. A reader whose cursor predates that value is told its cursor is stale
/// rather than handed a quietly incomplete answer — a change feed that silently drops changes is
/// worse than none, because consumers will trust it.
/// </summary>
internal sealed class ChangeLog(int capacity)
{
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly GraphChange[] _slots = new GraphChange[capacity];
    private readonly Dictionary<Handle, int> _slotOf = new(capacity);

    private int _next;      // the slot a genuinely new handle will take
    private int _used;      // slots filled so far, saturating at capacity
    private long _highestEvicted;

    public int Capacity => capacity;

    /// <summary>
    /// Records one write. Called only from the single writer thread (SWMR), but under the write
    /// lock regardless, because readers are concurrent.
    /// </summary>
    public void Record(Handle handle, ChangeKind kind, long revision)
    {
        _lock.EnterWriteLock();
        try
        {
            // Already here: update in place. No slot is consumed, so write volume cannot evict
            // anything -- the capacity is a budget of distinct handles.
            if (_slotOf.TryGetValue(handle, out int existing))
            {
                _slots[existing] = new GraphChange(revision, handle, kind | _slots[existing].Kind);
                return;
            }

            // A new handle at capacity displaces the longest-resident slot. Whatever it held is lost
            // to anyone who had not read it yet -- which is exactly what overflow means.
            if (_used == capacity)
            {
                var evicted = _slots[_next];
                if (evicted.Revision > _highestEvicted) _highestEvicted = evicted.Revision;
                _slotOf.Remove(evicted.Handle);
            }
            else
            {
                _used++;
            }

            _slots[_next] = new GraphChange(revision, handle, kind);
            _slotOf[handle] = _next;
            _next = (_next + 1) % capacity;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Every entry newer than <paramref name="revision"/>, oldest first. Slots are updated in place
    /// and therefore are not stored in revision order, so this sorts — see the class doc for why
    /// that trade lands on the read side.
    /// </summary>
    public IReadOnlyList<GraphChange> Since(long revision, out bool overflowed)
    {
        _lock.EnterReadLock();
        try
        {
            overflowed = _highestEvicted > revision;

            var result = new List<GraphChange>();
            for (int slot = 0; slot < _used; slot++)
            {
                if (_slots[slot].Revision > revision) result.Add(_slots[slot]);
            }
            result.Sort(static (x, y) => x.Revision.CompareTo(y.Revision));
            return result;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
}
