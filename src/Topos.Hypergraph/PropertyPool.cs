using System.Collections.Immutable;

namespace Topos.Hypergraph;

/// <summary>
/// A single property pool: a <see cref="SparseSet{T}"/> guarded by its own
/// <see cref="ReaderWriterLockSlim"/> — the spec §3.4 "per-pool, not global" granularity.
/// Concurrent readers of different properties never contend; writers to different properties
/// never contend either. Within one pool, reads run concurrently with each other but not with a
/// write.
/// </summary>
internal sealed class PropertyPool<T>
{
    private readonly SparseSet<T> _values = new();
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);

    public void Set(Handle handle, T value)
    {
        _lock.EnterWriteLock();
        try { _values.Set(handle, value); }
        finally { _lock.ExitWriteLock(); }
    }

    public bool TryGet(Handle handle, out T value)
    {
        _lock.EnterReadLock();
        try { return _values.TryGet(handle, out value); }
        finally { _lock.ExitReadLock(); }
    }

    public bool Remove(Handle handle)
    {
        _lock.EnterWriteLock();
        try { return _values.Remove(handle); }
        finally { _lock.ExitWriteLock(); }
    }

    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try { return _values.Count; }
            finally { _lock.ExitReadLock(); }
        }
    }

    /// <summary>Snapshot of every Handle currently in the pool — used by <see cref="HypergraphKernel"/>'s <c>IHypergraphQuery.VertexHandles</c>.</summary>
    public ImmutableArray<Handle> Handles
    {
        get
        {
            _lock.EnterReadLock();
            try { return [.. _values.DenseHandles]; }
            finally { _lock.ExitReadLock(); }
        }
    }
}
