using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

/// <summary>M13 §2 item 6 — the consumer-side payoff of the revision counter.</summary>
public class RevisionCacheTests
{
    [Fact]
    public void ComputesOnceThenServesFreeReads()
    {
        var kernel = new HypergraphKernel();
        kernel.CreateVertex();
        var cache = new RevisionCache<int>(kernel, k => k.CountVertices());

        Assert.Equal(1, cache.Value);
        Assert.Equal(1, cache.Value);
        Assert.Equal(1, cache.Value);

        Assert.Equal(1, cache.Recomputations);
    }

    [Fact]
    public void RecomputesAfterAWrite()
    {
        var kernel = new HypergraphKernel();
        kernel.CreateVertex();
        var cache = new RevisionCache<int>(kernel, k => k.CountVertices());

        Assert.Equal(1, cache.Value);
        kernel.CreateVertex();
        Assert.Equal(2, cache.Value);
        Assert.Equal(2, cache.Recomputations);
    }

    [Fact]
    public void DoesNotRecomputeForAWriteTheKernelTreatsAsANoOp()
    {
        var kernel = new HypergraphKernel();
        var a = kernel.CreateVertex();
        kernel.SetDormant(a);
        var cache = new RevisionCache<int>(kernel, k => k.CountVertices());

        _ = cache.Value;
        kernel.SetDormant(a);      // already dormant -- no revision movement
        _ = cache.Value;

        Assert.Equal(1, cache.Recomputations);
    }

    [Fact]
    public void TracksTheRevisionItComputedAt()
    {
        var kernel = new HypergraphKernel();
        var cache = new RevisionCache<int>(kernel, k => k.CountVertices());
        Assert.Equal(-1, cache.ComputedAtRevision);

        kernel.CreateVertex();
        _ = cache.Value;
        Assert.Equal(kernel.CurrentRevision, cache.ComputedAtRevision);
    }

    [Fact]
    public void InvalidateForcesARecompute()
    {
        var kernel = new HypergraphKernel();
        kernel.CreateVertex();
        var cache = new RevisionCache<int>(kernel, k => k.CountVertices());

        _ = cache.Value;
        cache.Invalidate();
        _ = cache.Value;

        Assert.Equal(2, cache.Recomputations);
    }

    [Fact]
    public void CachesAWholeGraphDerivationCorrectly()
    {
        // The intended use: something genuinely expensive over the whole graph.
        var kernel = new HypergraphKernel();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge, a, 0, 0);
        kernel.AddIncidence(edge, b, 0, 1);

        var ranking = new RevisionCache<IReadOnlyDictionary<Handle, double>>(kernel, k => PageRank.Compute(k));

        var first = ranking.Value;
        Assert.Same(first, ranking.Value);          // literally the same object -- no work done

        kernel.CreateVertex();
        Assert.NotSame(first, ranking.Value);       // the graph moved, so the ranking is rebuilt
        Assert.Equal(2, ranking.Recomputations);
    }
}
