using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

public class SWalkTests
{
    private static (HypergraphKernel Kernel, Handle E1, Handle E2, Handle E3) BuildChain()
    {
        // e1={a,b,c}, e2={b,c,d} (shares {b,c}=2 with e1), e3={d,e,f} (shares {d}=1 with e2, {}=0 with e1).
        var kernel = new HypergraphKernel();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var c = kernel.CreateVertex();
        var d = kernel.CreateVertex();
        var e = kernel.CreateVertex();
        var f = kernel.CreateVertex();

        var e1 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(e1, a, 0, 0); kernel.AddIncidence(e1, b, 0, 1); kernel.AddIncidence(e1, c, 0, 2);
        var e2 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(e2, b, 0, 0); kernel.AddIncidence(e2, c, 0, 1); kernel.AddIncidence(e2, d, 0, 2);
        var e3 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(e3, d, 0, 0); kernel.AddIncidence(e3, e, 0, 1); kernel.AddIncidence(e3, f, 0, 2);

        return (kernel, e1, e2, e3);
    }

    [Fact]
    public void Distance_AtS1_ReachesThroughTheWholeChain()
    {
        var (kernel, e1, _, e3) = BuildChain();
        Assert.Equal(2, SWalk.Distance(kernel, e1, e3, s: 1));
    }

    [Fact]
    public void Distance_AtS2_CannotCrossTheWeakLink()
    {
        // e2-e3 only share 1 member -- not enough for s=2 adjacency.
        var (kernel, e1, _, e3) = BuildChain();
        Assert.Null(SWalk.Distance(kernel, e1, e3, s: 2));
    }

    [Fact]
    public void Distance_AtS2_StillReachesTheStronglyLinkedNeighbor()
    {
        var (kernel, e1, e2, _) = BuildChain();
        Assert.Equal(1, SWalk.Distance(kernel, e1, e2, s: 2));
    }

    [Fact]
    public void Reachable_AtS1_FindsAllThreeEdges()
    {
        var (kernel, e1, e2, e3) = BuildChain();
        var reached = SWalk.Reachable(kernel, e1, s: 1).ToList();

        Assert.Contains(e1, reached);
        Assert.Contains(e2, reached);
        Assert.Contains(e3, reached);
    }

    [Fact]
    public void Reachable_AtS2_StopsAtTheWeakLink()
    {
        var (kernel, e1, e2, e3) = BuildChain();
        var reached = SWalk.Reachable(kernel, e1, s: 2).ToList();

        Assert.Contains(e1, reached);
        Assert.Contains(e2, reached);
        Assert.DoesNotContain(e3, reached);
    }

    [Fact]
    public void Distance_SameEdge_IsZero()
    {
        var (kernel, e1, _, _) = BuildChain();
        Assert.Equal(0, SWalk.Distance(kernel, e1, e1, s: 1));
    }

    [Fact]
    public void Distance_UnknownHandle_IsNull()
    {
        var kernel = new HypergraphKernel();
        Assert.Null(SWalk.Distance(kernel, new Handle(1), new Handle(2), s: 1));
    }

    [Fact]
    public void InvalidS_Throws()
    {
        var kernel = new HypergraphKernel();
        var h = kernel.CreateVertex();
        Assert.Throws<ArgumentOutOfRangeException>(() => SWalk.Distance(kernel, h, h, s: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SWalk.Reachable(kernel, h, s: 0).ToList());
    }
}
