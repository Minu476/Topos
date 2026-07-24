using System.Collections.Concurrent;
using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

public class HandleAllocatorTests
{
    [Fact]
    public void NeverReusesIndex()
    {
        var allocator = new HandleAllocator();
        var seen = new HashSet<uint>();
        for (int i = 0; i < 10_000; i++)
        {
            var h = allocator.Next();
            Assert.True(seen.Add(h.Index), $"Index {h.Index} was reused");
        }
    }

    [Fact]
    public void IsMonotonic()
    {
        var allocator = new HandleAllocator();
        uint last = 0;
        for (int i = 0; i < 1000; i++)
        {
            var h = allocator.Next();
            if (i > 0) Assert.True(h.Index > last);
            last = h.Index;
        }
    }

    [Fact]
    public async Task NeverReusesIndex_UnderConcurrency()
    {
        var allocator = new HandleAllocator();
        var bag = new ConcurrentBag<uint>();

        var tasks = Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 5000; i++) bag.Add(allocator.Next().Index);
        }));
        await Task.WhenAll(tasks);

        Assert.Equal(16 * 5000, bag.Count);
        Assert.Equal(bag.Count, bag.Distinct().Count());
    }
}
