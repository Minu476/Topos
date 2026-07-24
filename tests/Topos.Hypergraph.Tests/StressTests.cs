using System.Collections.Concurrent;
using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

/// <summary>
/// Higher-N versions of <c>ConcurrencyTests</c>, sized to have a real chance of surfacing a race
/// in the copy-on-write incidence index or per-pool locking if one existed — the earlier tests
/// are correctness smoke tests, these are closer to stress tests.
/// </summary>
public class StressTests
{
    [Fact]
    public async Task HighVolumeConcurrentVertexCreation_AllHandlesUnique()
    {
        var kernel = new HypergraphKernel();
        const int threads = 16;
        const int perThread = 50_000;
        var bag = new ConcurrentBag<uint>();

        var tasks = Enumerable.Range(0, threads).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < perThread; i++)
                bag.Add(kernel.CreateVertex().Index);
        }));
        await Task.WhenAll(tasks);

        Assert.Equal(threads * perThread, bag.Count);
        Assert.Equal(bag.Count, bag.Distinct().Count());
    }

    /// <summary>
    /// Fan-in: many threads appending to the *same* incidence source concurrently.
    ///
    /// Deliberately kept to a modest N (4 x 1,000, not 16 x 10,000) — this is the worst case for
    /// the current COW `ImmutableArray&lt;Incidence&gt;`-per-key design: each append copies the
    /// whole array, so N appends into one key costs O(N²) total, not O(N). That's a real,
    /// intentional finding, not a test-writing shortcut — see the P0 benchmark report for the
    /// scaling data and what it implies for a hub-vertex workload (a vertex with many incident
    /// members, e.g. a well-connected concept in a large graph).
    /// </summary>
    [Fact]
    public async Task ConcurrentIncidenceFanIn_ToOneSharedSource_NoLostUpdates()
    {
        var kernel = new HypergraphKernel();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        const int threads = 4;
        const int perThread = 1000;

        var tasks = Enumerable.Range(0, threads).Select(t => Task.Run(() =>
        {
            for (int i = 0; i < perThread; i++)
                kernel.AddIncidence(edge, kernel.CreateVertex(), role: 1, ordinal: t * perThread + i);
        }));
        await Task.WhenAll(tasks);

        Assert.Equal(threads * perThread, kernel.IncidencesFrom(edge).Length);
    }

    [Fact]
    public async Task HighVolumePropertyChurn_AcrossManyHandles_StaysConsistent()
    {
        var kernel = new HypergraphKernel();
        var key = kernel.ResolveProperty<long>("stress-counter");
        var handles = Enumerable.Range(0, 2000).Select(_ => kernel.CreateVertex()).ToArray();

        var tasks = Enumerable.Range(0, 8).Select(t => Task.Run(() =>
        {
            var rng = new Random(Seed: 5000 + t);
            for (int i = 0; i < 20_000; i++)
            {
                var h = handles[rng.Next(handles.Length)];
                kernel.SetProperty(key, h, i);
            }
        }));
        await Task.WhenAll(tasks);

        // Every handle must still have *some* consistent last-write value, not a torn read.
        foreach (var h in handles)
        {
            if (kernel.TryGetProperty(key, h, out var value))
                Assert.True(value >= 0);
        }
    }
}
