using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

public class SparseSetTests
{
    [Fact]
    public void SetThenGet_RoundTrips()
    {
        var set = new SparseSet<string>();
        var h = new Handle(3);

        set.Set(h, "hello");

        Assert.True(set.TryGet(h, out var value));
        Assert.Equal("hello", value);
    }

    [Fact]
    public void Set_OnExistingHandle_Overwrites()
    {
        var set = new SparseSet<int>();
        var h = new Handle(0);

        set.Set(h, 1);
        set.Set(h, 2);

        Assert.True(set.TryGet(h, out var value));
        Assert.Equal(2, value);
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void TryGet_OnUnknownHandle_ReturnsFalse()
    {
        var set = new SparseSet<int>();
        Assert.False(set.TryGet(new Handle(42), out _));
    }

    [Fact]
    public void Remove_SwapsWithLast_ButHandleLookupStaysCorrect()
    {
        var set = new SparseSet<int>();
        var a = new Handle(0);
        var b = new Handle(1);
        var c = new Handle(2);
        set.Set(a, 10);
        set.Set(b, 20);
        set.Set(c, 30);

        // Removing the middle element forces a swap-with-last in the *dense* array. This is not
        // the pattern spec §8 rejects ("swap-with-last removal — silently invalidates handles"):
        // that pattern used the array index as the externally-visible identity, so a swap
        // silently changed a live handle's meaning. Here the sparse indirection absorbs the
        // move — every remaining Handle must still resolve to its correct value.
        Assert.True(set.Remove(b));

        Assert.False(set.Contains(b));
        Assert.True(set.TryGet(a, out var av));
        Assert.Equal(10, av);
        Assert.True(set.TryGet(c, out var cv));
        Assert.Equal(30, cv);
        Assert.Equal(2, set.Count);
    }

    [Fact]
    public void Remove_LastElement_DoesNotCorruptState()
    {
        var set = new SparseSet<int>();
        var a = new Handle(0);
        set.Set(a, 10);

        Assert.True(set.Remove(a));

        Assert.Equal(0, set.Count);
        Assert.False(set.Contains(a));
    }

    [Fact]
    public void Remove_UnknownHandle_ReturnsFalse()
    {
        var set = new SparseSet<int>();
        Assert.False(set.Remove(new Handle(99)));
    }

    [Fact]
    public void DenseIteration_IsContiguousAndMatchesCount()
    {
        var set = new SparseSet<int>();
        for (uint i = 0; i < 100; i++) set.Set(new Handle(i), (int)i * 2);

        Assert.Equal(100, set.Count);
        Assert.Equal(100, set.DenseValues.Length);
        Assert.Equal(100, set.DenseHandles.Length);

        for (int i = 0; i < set.DenseHandles.Length; i++)
        {
            Assert.True(set.TryGet(set.DenseHandles[i], out var v));
            Assert.Equal(v, set.DenseValues[i]);
        }
    }

    [Fact]
    public void SparseHandles_GrowsCorrectly_ForNonContiguousIndices()
    {
        var set = new SparseSet<string>();
        var sparse = new Handle(10_000);

        set.Set(sparse, "far");

        Assert.True(set.TryGet(sparse, out var value));
        Assert.Equal("far", value);
        Assert.Equal(1, set.Count);
    }
}
