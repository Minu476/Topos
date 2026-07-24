using System.Collections.Concurrent;
using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

/// <summary>
/// Exercises the spec §3.4 concurrency model: a single writer thread mutating while multiple
/// reader threads observe, plus the incidence indexes' CAS-based safety under concurrent writers.
/// </summary>
public class ConcurrencyTests
{
    [Fact]
    public async Task ConcurrentReaders_DuringSingleWriter_NeverObserveCorruption()
    {
        var kernel = new HypergraphKernel();
        var handles = new ConcurrentQueue<Handle>();

        var writer = Task.Run(() =>
        {
            for (int i = 0; i < 20_000; i++)
                handles.Enqueue(kernel.CreateVertex());
        });

        var readerErrors = new ConcurrentBag<Exception>();
        var readers = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            try
            {
                for (int pass = 0; pass < 20; pass++)
                {
                    foreach (var h in handles)
                    {
                        if (kernel.TryGetVertex(h, out var v))
                        {
                            var touched = v.Roles; // touch the record; a torn read would surface here
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                readerErrors.Add(ex);
            }
        }));

        await Task.WhenAll(readers.Append(writer));

        Assert.Empty(readerErrors);
        Assert.Equal(20_000, handles.Count);
    }

    [Fact]
    public async Task ConcurrentIncidenceAppends_AcrossManyWriters_AreNeverLost()
    {
        var kernel = new HypergraphKernel();
        var edge = kernel.CreateVertex(VertexRoles.Edge);

        // AddIncidence's ConcurrentDictionary.AddOrUpdate is CAS-safe even without the SWMR
        // single-writer assumption — this test intentionally uses multiple concurrent writers to
        // confirm no update is lost under contention on the same incidence-index key.
        var tasks = Enumerable.Range(0, 8).Select(t => Task.Run(() =>
        {
            for (int i = 0; i < 1000; i++)
                kernel.AddIncidence(edge, kernel.CreateVertex(), role: 0, ordinal: t * 1000 + i);
        }));
        await Task.WhenAll(tasks);

        Assert.Equal(8000, kernel.IncidencesFrom(edge).Length);
    }

    [Fact]
    public async Task ConcurrentPropertyPoolAccess_AcrossDifferentPools_DoesNotContend()
    {
        var kernel = new HypergraphKernel();
        var keyA = kernel.ResolveProperty<int>("a");
        var keyB = kernel.ResolveProperty<string>("b");
        var handle = kernel.CreateVertex();

        var writerA = Task.Run(() =>
        {
            for (int i = 0; i < 10_000; i++) kernel.SetProperty(keyA, handle, i);
        });
        var writerB = Task.Run(() =>
        {
            for (int i = 0; i < 10_000; i++) kernel.SetProperty(keyB, handle, i.ToString());
        });

        await Task.WhenAll(writerA, writerB);

        Assert.True(kernel.TryGetProperty(keyA, handle, out var a));
        Assert.True(kernel.TryGetProperty(keyB, handle, out var b));
        Assert.Equal(9999, a);
        Assert.Equal("9999", b);
    }
}
