namespace Topos.Hypergraph;

/// <summary>
/// EnTT-style sparse-set columnar pool (spec §3: "PropertyBag (columnar, EnTT-style sparse-set
/// pools)"). O(1) add/remove/lookup; the dense array supports contiguous iteration for cache-
/// friendly traversal. A growable sparse array maps <see cref="Handle.Index"/> to a slot in the
/// dense arrays.
///
/// <see cref="Remove"/> uses swap-with-last on the *dense* array only. This is deliberately not
/// the pattern rejected in spec §8 ("swap-with-last removal (Julia) — silently invalidates
/// handles; disqualifying"): that pattern used the array index itself as the externally-visible
/// identity, so a swap silently changed a live Handle's meaning. Here the sparse indirection
/// layer absorbs the move — a Handle's value never changes, only its internal dense-array slot,
/// which <see cref="Remove"/> re-indexes before returning. See <c>SparseSetTests</c> for the
/// regression test.
///
/// Not thread-safe on its own — the owning pool applies the spec §3.4 per-pool lock
/// (<see cref="PropertyPool{T}"/>).
///
/// <b>Internal, not public — locked M8.</b> Matches the convention of its sibling plumbing
/// (<see cref="PropertyPool{T}"/>, <c>IncidenceIndex</c>): the kernel's public surface is
/// <see cref="IHypergraphQuery"/> and <see cref="HypergraphKernel"/>, not its storage internals.
/// Test and benchmark projects that need direct access do so via
/// <c>[InternalsVisibleTo]</c> (see <c>Topos.Hypergraph.csproj</c>), not public exposure.
/// </summary>
internal sealed class SparseSet<T>
{
    private const uint Tombstone = uint.MaxValue;

    private uint[] _sparse = [];
    private Handle[] _denseHandles = [];
    private T[] _denseValues = [];
    private int _count;

    public int Count => _count;

    public ReadOnlySpan<Handle> DenseHandles => _denseHandles.AsSpan(0, _count);
    public ReadOnlySpan<T> DenseValues => _denseValues.AsSpan(0, _count);

    public bool Contains(Handle handle)
    {
        uint idx = handle.Index;
        return idx < _sparse.Length
            && _sparse[idx] != Tombstone
            && _denseHandles[(int)_sparse[idx]] == handle;
    }

    public void Set(Handle handle, T value)
    {
        uint idx = handle.Index;
        EnsureSparseCapacity(idx);

        if (_sparse[idx] != Tombstone && _denseHandles[(int)_sparse[idx]] == handle)
        {
            _denseValues[(int)_sparse[idx]] = value;
            return;
        }

        EnsureDenseCapacity(_count + 1);
        _sparse[idx] = (uint)_count;
        _denseHandles[_count] = handle;
        _denseValues[_count] = value;
        _count++;
    }

    public bool TryGet(Handle handle, out T value)
    {
        if (Contains(handle))
        {
            value = _denseValues[(int)_sparse[handle.Index]];
            return true;
        }

        value = default!;
        return false;
    }

    public bool Remove(Handle handle)
    {
        if (!Contains(handle)) return false;

        int slot = (int)_sparse[handle.Index];
        int last = _count - 1;

        _denseHandles[slot] = _denseHandles[last];
        _denseValues[slot] = _denseValues[last];
        _sparse[_denseHandles[slot].Index] = (uint)slot;

        _denseHandles[last] = default;
        _denseValues[last] = default!;
        _sparse[handle.Index] = Tombstone;
        _count--;
        return true;
    }

    private void EnsureSparseCapacity(uint idx)
    {
        if (idx < _sparse.Length) return;

        int newSize = Math.Max((int)idx + 1, _sparse.Length == 0 ? 16 : _sparse.Length * 2);
        var next = new uint[newSize];
        Array.Fill(next, Tombstone);
        _sparse.AsSpan().CopyTo(next);
        _sparse = next;
    }

    private void EnsureDenseCapacity(int required)
    {
        if (required <= _denseHandles.Length) return;

        int newSize = _denseHandles.Length == 0 ? 16 : _denseHandles.Length * 2;
        Array.Resize(ref _denseHandles, newSize);
        Array.Resize(ref _denseValues, newSize);
    }
}
