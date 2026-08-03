using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

public class PageRankTests
{
    [Fact]
    public void SumsToOne_AcrossAnyGraphShape()
    {
        // Property #7 from docs/ALGORITHM_GAP_LIST.md's metamorphic-testing list: PageRank must
        // sum to 1 across all vertices regardless of damping/dangling mass.
        var kernel = new HypergraphKernel();
        var e1 = kernel.CreateVertex(VertexRoles.Edge);
        var a = kernel.CreateVertex(); var b = kernel.CreateVertex();
        kernel.AddIncidence(e1, a, 0, 0); kernel.AddIncidence(e1, b, 0, 1);
        var e2 = kernel.CreateVertex(VertexRoles.Edge);
        var c = kernel.CreateVertex();
        kernel.AddIncidence(e2, b, 0, 0); kernel.AddIncidence(e2, c, 0, 1);

        var rank = PageRank.Compute(kernel);

        Assert.Equal(1.0, rank.Values.Sum(), precision: 8);
    }

    [Fact]
    public void SingleTwoMemberHyperedge_UniformAcrossTheResultingTriangle()
    {
        // A 2-member hyperedge forms a triangle {edge,a,b} under BipartiteAdjacency (TriangleCount's
        // own minimal shape) -- vertex-transitive, so PageRank must converge to exactly 1/3 each.
        var kernel = new HypergraphKernel();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        kernel.AddIncidence(edge, a, 0, 0);
        kernel.AddIncidence(edge, b, 0, 1);

        var rank = PageRank.Compute(kernel);

        Assert.Equal(1.0 / 3.0, rank[edge], precision: 6);
        Assert.Equal(1.0 / 3.0, rank[a], precision: 6);
        Assert.Equal(1.0 / 3.0, rank[b], precision: 6);
    }

    [Fact]
    public void K4_ConvergesToUniformQuarterEach()
    {
        var kernel = new HypergraphKernel();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var c = kernel.CreateVertex();
        kernel.AddIncidence(edge, a, 0, 0);
        kernel.AddIncidence(edge, b, 0, 1);
        kernel.AddIncidence(edge, c, 0, 2);

        var rank = PageRank.Compute(kernel);

        Assert.Equal(0.25, rank[edge], precision: 6);
        Assert.Equal(0.25, rank[a], precision: 6);
        Assert.Equal(0.25, rank[b], precision: 6);
        Assert.Equal(0.25, rank[c], precision: 6);
    }

    [Fact]
    public void Bowtie_MatchesHandSolvedFixedPoint()
    {
        // Two triangles {e1,a,b}/{e2,b,c} sharing b (same shape as CentralityTests' bowtie).
        // Solving PR(v) = (1-d)/N + d*sum(PR(u)/deg(u)) by hand for d=0.85, N=5 (exploiting the
        // graph's mirror symmetry: PR(e1)=PR(a)=PR(e2)=PR(c)=p, PR(b)=q, 4p+q=1) gives
        // p = 97/570 and q = 3/100 + 1.7p -- see PageRankTests' derivation in the PR description.
        var kernel = new HypergraphKernel();
        var e1 = kernel.CreateVertex(VertexRoles.Edge);
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        kernel.AddIncidence(e1, a, 0, 0);
        kernel.AddIncidence(e1, b, 0, 1);

        var e2 = kernel.CreateVertex(VertexRoles.Edge);
        var c = kernel.CreateVertex();
        kernel.AddIncidence(e2, b, 0, 0);
        kernel.AddIncidence(e2, c, 0, 1);

        var rank = PageRank.Compute(kernel);

        const double p = 97.0 / 570.0;
        double q = 1.0 - 4 * p;

        Assert.Equal(p, rank[e1], precision: 5);
        Assert.Equal(p, rank[a], precision: 5);
        Assert.Equal(q, rank[b], precision: 5);
        Assert.Equal(p, rank[e2], precision: 5);
        Assert.Equal(p, rank[c], precision: 5);
        Assert.True(rank[b] > rank[a]); // the shared/hub vertex must rank highest
    }

    [Fact]
    public void EmptyGraph_ReturnsEmptyResult()
    {
        var kernel = new HypergraphKernel();
        Assert.Empty(PageRank.Compute(kernel));
    }

    [Fact]
    public void IsolatedVertex_GetsFullDanglingMassRedistributedBackToItself()
    {
        var kernel = new HypergraphKernel();
        var isolated = kernel.CreateVertex();

        var rank = PageRank.Compute(kernel);

        Assert.Equal(1.0, rank[isolated], precision: 8);
    }
}
