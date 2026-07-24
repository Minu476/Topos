using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

public class ShortestPathTests
{
    [Fact]
    public void GetShortestPathLength_SameVertex_IsZero()
    {
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var a = raw.CreateVertex();

        Assert.Equal(0, kernel.GetShortestPathLength(a, a));
    }

    [Fact]
    public void GetShortestPathLength_UnreachableVertex_IsNull()
    {
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var a = raw.CreateVertex();
        var isolated = raw.CreateVertex();

        Assert.Null(kernel.GetShortestPathLength(a, isolated));
    }

    [Fact]
    public void GetShortestPathLength_UnknownHandle_IsNull()
    {
        IHypergraphQuery kernel = new HypergraphKernel();
        Assert.Null(kernel.GetShortestPathLength(new Handle(1), new Handle(2)));
    }

    [Fact]
    public void GetShortestPathLength_OnChain_CountsHopsNotEdgeNodes()
    {
        // a --edge1--> b --edge2--> c --edge3--> d : 3 logical hops from a to d.
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var a = raw.CreateVertex();
        var b = raw.CreateVertex();
        var c = raw.CreateVertex();
        var d = raw.CreateVertex();
        var e1 = raw.CreateVertex(VertexRoles.Edge);
        var e2 = raw.CreateVertex(VertexRoles.Edge);
        var e3 = raw.CreateVertex(VertexRoles.Edge);
        raw.AddIncidence(e1, a, 0, 0); raw.AddIncidence(e1, b, 2, 1);
        raw.AddIncidence(e2, b, 0, 0); raw.AddIncidence(e2, c, 2, 1);
        raw.AddIncidence(e3, c, 0, 0); raw.AddIncidence(e3, d, 2, 1);

        Assert.Equal(1, kernel.GetShortestPathLength(a, b));
        Assert.Equal(2, kernel.GetShortestPathLength(a, c));
        Assert.Equal(3, kernel.GetShortestPathLength(a, d));
    }

    [Fact]
    public void GetShortestPathLength_TakesShorterOfTwoRoutes()
    {
        // a direct-connected to c via one edge (1 hop), and via a longer a-b-c chain (2 hops).
        // Shortest must be 1, not 2.
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var a = raw.CreateVertex();
        var b = raw.CreateVertex();
        var c = raw.CreateVertex();
        var shortEdge = raw.CreateVertex(VertexRoles.Edge);
        var e1 = raw.CreateVertex(VertexRoles.Edge);
        var e2 = raw.CreateVertex(VertexRoles.Edge);
        raw.AddIncidence(shortEdge, a, 0, 0); raw.AddIncidence(shortEdge, c, 2, 1);
        raw.AddIncidence(e1, a, 0, 0); raw.AddIncidence(e1, b, 2, 1);
        raw.AddIncidence(e2, b, 0, 0); raw.AddIncidence(e2, c, 2, 1);

        Assert.Equal(1, kernel.GetShortestPathLength(a, c));
    }

    [Fact]
    public void GetShortestPath_ReturnsEndpointsInclusive_AndMatchesLength()
    {
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var a = raw.CreateVertex();
        var b = raw.CreateVertex();
        var c = raw.CreateVertex();
        var e1 = raw.CreateVertex(VertexRoles.Edge);
        var e2 = raw.CreateVertex(VertexRoles.Edge);
        raw.AddIncidence(e1, a, 0, 0); raw.AddIncidence(e1, b, 2, 1);
        raw.AddIncidence(e2, b, 0, 0); raw.AddIncidence(e2, c, 2, 1);

        var path = kernel.GetShortestPath(a, c);

        Assert.Equal([a, b, c], path);
        Assert.Equal(path.Count - 1, kernel.GetShortestPathLength(a, c));
    }

    [Fact]
    public void GetShortestPath_SameVertex_ReturnsSingleElement()
    {
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var a = raw.CreateVertex();

        Assert.Equal([a], kernel.GetShortestPath(a, a));
    }

    [Fact]
    public void GetShortestPath_Unreachable_ReturnsEmpty()
    {
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var a = raw.CreateVertex();
        var isolated = raw.CreateVertex();

        Assert.Empty(kernel.GetShortestPath(a, isolated));
    }
}
