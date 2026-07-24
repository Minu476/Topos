namespace Topos.Hypergraph.Persistence;

/// <summary>
/// Classic O(1) LRU cache — <c>Dictionary&lt;TKey, Node&gt;</c> for O(1) lookup plus an
/// intrusive doubly-linked list for O(1) move-to-front and O(1) evict-oldest. The hot-tier
/// building block for spec §6 M4 ("hot LRU + cold LSM... hot-tier lookup is O(1)").
///
/// Not thread-safe on its own — a caller needing concurrent access should guard it the same way
/// <c>PropertyPool&lt;T&gt;</c> guards its <c>SparseSet&lt;T&gt;</c> (spec §3.4's per-pool
/// <see cref="ReaderWriterLockSlim"/> pattern), rather than this class taking on locking it may
/// not need for every use case.
/// </summary>
public sealed class LruCache<TKey, TValue> where TKey : notnull
{
    private sealed class Node
    {
        public required TKey Key;
        public required TValue Value;
        public Node? Prev;
        public Node? Next;
    }

    private readonly Dictionary<TKey, Node> _index = [];
    private Node? _head; // most recently used
    private Node? _tail; // least recently used

    public LruCache(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
        Capacity = capacity;
    }

    public int Capacity { get; }

    public int Count => _index.Count;

    public bool TryGet(TKey key, out TValue value)
    {
        if (!_index.TryGetValue(key, out var node))
        {
            value = default!;
            return false;
        }

        MoveToFront(node);
        value = node.Value;
        return true;
    }

    public bool ContainsKey(TKey key) => _index.ContainsKey(key);

    /// <summary>Inserts or updates a key. Returns the evicted (key, value) pair if this insertion caused an eviction, or null otherwise — the eviction is the caller's cue to write that entry to the cold tier before it's gone from the hot one.</summary>
    public (TKey Key, TValue Value)? Set(TKey key, TValue value)
    {
        if (_index.TryGetValue(key, out var existing))
        {
            existing.Value = value;
            MoveToFront(existing);
            return null;
        }

        var node = new Node { Key = key, Value = value };
        _index[key] = node;
        AddToFront(node);

        if (_index.Count > Capacity)
        {
            var evicted = _tail!;
            RemoveNode(evicted);
            _index.Remove(evicted.Key);
            return (evicted.Key, evicted.Value);
        }

        return null;
    }

    public bool Remove(TKey key)
    {
        if (!_index.TryGetValue(key, out var node)) return false;
        RemoveNode(node);
        _index.Remove(key);
        return true;
    }

    private void MoveToFront(Node node)
    {
        if (node == _head) return;
        RemoveNode(node);
        AddToFront(node);
    }

    private void AddToFront(Node node)
    {
        node.Prev = null;
        node.Next = _head;
        if (_head is not null) _head.Prev = node;
        _head = node;
        _tail ??= node;
    }

    private void RemoveNode(Node node)
    {
        if (node.Prev is not null) node.Prev.Next = node.Next;
        else _head = node.Next;

        if (node.Next is not null) node.Next.Prev = node.Prev;
        else _tail = node.Prev;

        node.Prev = null;
        node.Next = null;
    }
}
