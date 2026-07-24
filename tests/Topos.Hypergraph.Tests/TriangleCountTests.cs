using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

public class TriangleCountTests
{
    [Fact]
    public void EmptyGraph_HasZeroTriangles()
    {
        var kernel = new HypergraphKernel();
        Assert.Equal(0, TriangleCount.Count(kernel));
    }

    [Fact]
    public void TwoIsolatedVertices_NoTriangles()
    {
        var kernel = new HypergraphKernel();
        kernel.CreateVertex();
        kernel.CreateVertex();
        Assert.Equal(0, TriangleCount.Count(kernel));
    }

    [Fact]
    public void SingleThreeMemberHyperedge_ProducesFourTriangles()
    {
        // Documented, deliberate consequence (see TriangleCount's class doc): the hyperedge
        // vertex is bipartite-connected to all three members, forming K4 -- C(4,3) = 4 triangles
        // from one hyperedge, not just the 1 "real" triangle among the members alone.
        var kernel = new HypergraphKernel();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var c = kernel.CreateVertex();
        kernel.AddIncidence(edge, a, 0, 0);
        kernel.AddIncidence(edge, b, 0, 1);
        kernel.AddIncidence(edge, c, 0, 2);

        Assert.Equal(4, TriangleCount.Count(kernel));
    }

    [Fact]
    public void TwoMemberHyperedge_AlreadyFormsOneTriangleWithItsOwnEdgeVertex()
    {
        // Not "a path of length 2" as might be assumed -- a and b are directly co-member-adjacent
        // to each other AND each is adjacent to the edge-vertex, so {a, b, edge} is a triangle
        // even at the minimum (2-member, RLB's D2 with zero Conditions) hyperedge shape. See
        // TriangleCount's class doc for the general C(N+1,3) rule this is the N=2 case of.
        var kernel = new HypergraphKernel();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        kernel.AddIncidence(edge, a, 0, 0);
        kernel.AddIncidence(edge, b, 0, 1);

        Assert.Equal(1, TriangleCount.Count(kernel));
    }

    [Fact]
    public void TwoDisjointThreeMemberHyperedges_CountsAreIndependent()
    {
        var kernel = new HypergraphKernel();
        var edge1 = kernel.CreateVertex(VertexRoles.Edge);
        var a = kernel.CreateVertex(); var b = kernel.CreateVertex(); var c = kernel.CreateVertex();
        kernel.AddIncidence(edge1, a, 0, 0); kernel.AddIncidence(edge1, b, 0, 1); kernel.AddIncidence(edge1, c, 0, 2);

        var edge2 = kernel.CreateVertex(VertexRoles.Edge);
        var x = kernel.CreateVertex(); var y = kernel.CreateVertex(); var z = kernel.CreateVertex();
        kernel.AddIncidence(edge2, x, 0, 0); kernel.AddIncidence(edge2, y, 0, 1); kernel.AddIncidence(edge2, z, 0, 2);

        Assert.Equal(8, TriangleCount.Count(kernel)); // 4 + 4, no interaction between them
    }
}
