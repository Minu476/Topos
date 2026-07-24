using System.Collections.Concurrent;
using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

/// <summary>
/// Multi-threaded, seeded-RNG interleaving of every write operation
/// <see cref="HypergraphKernel"/> exposes, checked against the spec §3 invariants afterward.
/// Each thread only mutates the vertices *it* created (honoring the SWMR single-writer-per-entity
/// discipline from spec §3.4) while still hammering shared kernel-wide state — the handle
/// allocator, the incidence indexes, and the per-pool locks — concurrently across threads.
/// </summary>
public class KernelFuzzTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(99)]
    public async Task InterleavedOps_AcrossThreads_NeverViolateInvariants(int seed)
    {
        var kernel = new HypergraphKernel();
        var allHandles = new ConcurrentBag<Handle>();
        var confidence = kernel.ResolveProperty<double>("fuzz-confidence");

        const int threads = 6;
        const int opsPerThread = 2000;

        var tasks = Enumerable.Range(0, threads).Select(t => Task.Run(() =>
        {
            var rng = new Random(seed * 1000 + t);
            var localHandles = new List<Handle>();

            for (int i = 0; i < opsPerThread; i++)
            {
                switch (rng.Next(0, 5))
                {
                    case 0:
                        var h = kernel.CreateVertex(rng.Next(0, 2) == 0 ? VertexRoles.None : VertexRoles.Edge);
                        localHandles.Add(h);
                        allHandles.Add(h);
                        break;

                    case 1 when localHandles.Count > 0:
                        kernel.SetDormant(localHandles[rng.Next(localHandles.Count)]);
                        break;

                    case 2 when localHandles.Count > 0:
                        kernel.Reactivate(localHandles[rng.Next(localHandles.Count)]);
                        break;

                    case 3 when localHandles.Count > 1:
                        var a = localHandles[rng.Next(localHandles.Count)];
                        var b = localHandles[rng.Next(localHandles.Count)];
                        kernel.AddIncidence(a, b, role: (byte)rng.Next(0, 3), ordinal: i);
                        break;

                    case 4 when localHandles.Count > 0:
                        kernel.SetProperty(confidence, localHandles[rng.Next(localHandles.Count)], rng.NextDouble());
                        break;
                }
            }
        }));

        await Task.WhenAll(tasks);

        // Post-hoc invariant checks (spec §3).
        var seen = new HashSet<uint>();
        foreach (var h in allHandles)
        {
            Assert.True(seen.Add(h.Index), $"Index {h.Index} was allocated more than once");
            // Invariant 1: every handle ever minted resolves forever, dormant or not.
            Assert.True(kernel.TryGetVertex(h, out _), $"Handle {h} no longer resolves");
        }
    }
}
