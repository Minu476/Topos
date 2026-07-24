using Topos.Hypergraph.Persistence;

namespace Topos.Hypergraph.Persistence.Tests;

public class LruCacheTests
{
    [Fact]
    public void SetThenGet_RoundTrips()
    {
        var cache = new LruCache<string, int>(capacity: 3);
        cache.Set("a", 1);

        Assert.True(cache.TryGet("a", out var value));
        Assert.Equal(1, value);
    }

    [Fact]
    public void TryGet_UnknownKey_ReturnsFalse()
    {
        var cache = new LruCache<string, int>(capacity: 3);
        Assert.False(cache.TryGet("missing", out _));
    }

    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LruCache<string, int>(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LruCache<string, int>(-1));
    }

    [Fact]
    public void Set_UpdatingExistingKey_DoesNotGrowCountOrEvict()
    {
        var cache = new LruCache<string, int>(capacity: 2);
        cache.Set("a", 1);
        var evicted = cache.Set("a", 2);

        Assert.Null(evicted);
        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGet("a", out var v));
        Assert.Equal(2, v);
    }

    [Fact]
    public void Set_BeyondCapacity_EvictsLeastRecentlyUsed()
    {
        var cache = new LruCache<string, int>(capacity: 2);
        cache.Set("a", 1);
        cache.Set("b", 2);
        var evicted = cache.Set("c", 3); // "a" is oldest, untouched since insertion

        Assert.Equal(("a", 1), evicted);
        Assert.False(cache.ContainsKey("a"));
        Assert.True(cache.ContainsKey("b"));
        Assert.True(cache.ContainsKey("c"));
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void TryGet_RefreshesRecency_ProtectingFromEviction()
    {
        var cache = new LruCache<string, int>(capacity: 2);
        cache.Set("a", 1);
        cache.Set("b", 2);

        cache.TryGet("a", out _); // touch "a" -- "b" is now the least-recently-used one

        var evicted = cache.Set("c", 3);

        Assert.Equal(("b", 2), evicted);
        Assert.True(cache.ContainsKey("a"));
        Assert.True(cache.ContainsKey("c"));
    }

    [Fact]
    public void Remove_ExistingKey_ReturnsTrueAndRemoves()
    {
        var cache = new LruCache<string, int>(capacity: 3);
        cache.Set("a", 1);

        Assert.True(cache.Remove("a"));
        Assert.False(cache.ContainsKey("a"));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void Remove_UnknownKey_ReturnsFalse()
    {
        var cache = new LruCache<string, int>(capacity: 3);
        Assert.False(cache.Remove("missing"));
    }

    [Fact]
    public void CapacityOne_AlwaysEvictsThePreviousEntry()
    {
        var cache = new LruCache<string, int>(capacity: 1);
        cache.Set("a", 1);
        var evicted = cache.Set("b", 2);

        Assert.Equal(("a", 1), evicted);
        Assert.Equal(1, cache.Count);
        Assert.True(cache.ContainsKey("b"));
    }

    [Fact]
    public void ManyEvictions_LeaveCacheInConsistentState()
    {
        // Stress the linked-list bookkeeping specifically -- repeated eviction is where an
        // off-by-one in Prev/Next relinking would show up as a corrupted chain.
        var cache = new LruCache<int, int>(capacity: 5);
        for (int i = 0; i < 100; i++) cache.Set(i, i * 10);

        Assert.Equal(5, cache.Count);
        for (int i = 95; i < 100; i++)
        {
            Assert.True(cache.TryGet(i, out var v));
            Assert.Equal(i * 10, v);
        }
        for (int i = 0; i < 95; i++)
            Assert.False(cache.ContainsKey(i));
    }
}
