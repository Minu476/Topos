using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

/// <summary>
/// M2 exit criterion (spec §6): "Recursive hypergraph works; nested reification depth-N
/// round-trips." Role:Edge vertices and Incidence.Role already existed from M0 — these tests
/// verify the thing that was never actually checked: that a reified edge can itself be a member
/// of another edge, at arbitrary depth, and every level still resolves correctly.
/// </summary>
public class ReificationTests
{
    [Fact]
    public void Depth1_PlainHyperedge_RoundTrips()
    {
        var kernel = new HypergraphKernel();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        var x = kernel.CreateVertex();
        var y = kernel.CreateVertex();
        kernel.AddIncidence(edge, x, role: 0, ordinal: 0);
        kernel.AddIncidence(edge, y, role: 2, ordinal: 1);

        Assert.Equal(2, kernel.IncidencesFrom(edge).Length);
        Assert.True(kernel.TryGetVertex(edge, out var v));
        Assert.Equal(VertexRoles.Edge, v.Roles);
    }

    [Fact]
    public void Depth2_EdgeAsMemberOfAnotherEdge_RoundTripsBothDirections()
    {
        // edgeA reifies a relationship between X and Y. edgeB reifies a relationship that
        // includes edgeA itself as a member (e.g. "Z quotes the statement edgeA makes").
        var kernel = new HypergraphKernel();
        var x = kernel.CreateVertex();
        var y = kernel.CreateVertex();
        var z = kernel.CreateVertex();
        var edgeA = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edgeA, x, role: 0, ordinal: 0);
        kernel.AddIncidence(edgeA, y, role: 2, ordinal: 1);

        var edgeB = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edgeB, edgeA, role: 0, ordinal: 0); // edgeA is a MEMBER here
        kernel.AddIncidence(edgeB, z, role: 2, ordinal: 1);

        // Forward: edgeB's members include edgeA.
        Assert.Contains(kernel.IncidencesFrom(edgeB), i => i.Member == edgeA);

        // Reverse: edgeA now knows it's a member of edgeB, on top of its own edge-hood.
        Assert.Contains(kernel.IncidencesOf(edgeA), i => i.Source == edgeB);

        // Dual role, no interference: edgeA is simultaneously an edge (still has its own X, Y
        // members) AND a member of edgeB. The two Incidence indexes (_bySource / _byMember) are
        // independent, so neither view disturbs the other.
        Assert.Equal(2, kernel.IncidencesFrom(edgeA).Length);
        Assert.True(kernel.TryGetVertex(edgeA, out var edgeAVertex));
        Assert.Equal(VertexRoles.Edge, edgeAVertex.Roles);
    }

    [Fact]
    public void DepthN_ChainOfNestedEdges_EveryLevelRoundTrips()
    {
        // edge1(leaf0, leaf1) -> edge2(edge1, leaf2) -> edge3(edge2, leaf3) -> edge4(edge3, leaf4)
        // : depth-4 nesting, each level wrapping the previous edge plus one new leaf.
        const int depth = 4;
        var kernel = new HypergraphKernel();

        var leaves = new List<Handle>();
        for (int i = 0; i <= depth; i++) leaves.Add(kernel.CreateVertex());

        var edges = new List<Handle>();
        var firstEdge = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(firstEdge, leaves[0], role: 0, ordinal: 0);
        kernel.AddIncidence(firstEdge, leaves[1], role: 2, ordinal: 1);
        edges.Add(firstEdge);

        for (int i = 2; i <= depth; i++)
        {
            var nextEdge = kernel.CreateVertex(VertexRoles.Edge);
            kernel.AddIncidence(nextEdge, edges[^1], role: 0, ordinal: 0); // nests the prior edge
            kernel.AddIncidence(nextEdge, leaves[i], role: 2, ordinal: 1);
            edges.Add(nextEdge);
        }

        // Every level: the wrapping edge's members correctly include the nested edge, and the
        // nested edge correctly reports the wrapper as one of its own memberships.
        for (int i = 0; i < edges.Count - 1; i++)
        {
            Assert.Contains(kernel.IncidencesFrom(edges[i + 1]), m => m.Member == edges[i]);
            Assert.Contains(kernel.IncidencesOf(edges[i]), m => m.Source == edges[i + 1]);
        }

        // The innermost edge still resolves its own original members, unaffected by three
        // further layers of nesting on top of it.
        Assert.Contains(kernel.IncidencesFrom(edges[0]), m => m.Member == leaves[0]);
        Assert.Contains(kernel.IncidencesFrom(edges[0]), m => m.Member == leaves[1]);

        // Every edge in the chain is still a Vertex tagged Edge, at every depth.
        foreach (var edge in edges)
        {
            Assert.True(kernel.TryGetVertex(edge, out var v));
            Assert.Equal(VertexRoles.Edge, v.Roles);
        }
    }

    [Fact]
    public void AssertionMode_RoundTripsIndependentlyOfNestingDepth()
    {
        var kernel = new HypergraphKernel();
        var mode = kernel.ResolveProperty<AssertionMode>("mode");

        var x = kernel.CreateVertex();
        var y = kernel.CreateVertex();
        var edgeA = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edgeA, x, 0, 0);
        kernel.AddIncidence(edgeA, y, 2, 1);
        kernel.SetProperty(mode, edgeA, AssertionMode.Hypothesized);

        var z = kernel.CreateVertex();
        var edgeB = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edgeB, edgeA, 0, 0);
        kernel.AddIncidence(edgeB, z, 2, 1);
        kernel.SetProperty(mode, edgeB, AssertionMode.Quoted);

        // Each edge's mode is independent of the other's, and independent of nesting structure.
        Assert.True(kernel.TryGetProperty(mode, edgeA, out var edgeAMode));
        Assert.Equal(AssertionMode.Hypothesized, edgeAMode);
        Assert.True(kernel.TryGetProperty(mode, edgeB, out var edgeBMode));
        Assert.Equal(AssertionMode.Quoted, edgeBMode);
    }

    [Fact]
    public void AssertionMode_UnsetOnAVertex_ReturnsFalseNotADefaultValue()
    {
        // No mode recorded means exactly that -- not an implicit Asserted default. The kernel
        // doesn't judge; TryGetProperty's bool is the honest signal, same as any other property.
        var kernel = new HypergraphKernel();
        var mode = kernel.ResolveProperty<AssertionMode>("mode");
        var edge = kernel.CreateVertex(VertexRoles.Edge);

        Assert.False(kernel.TryGetProperty(mode, edge, out _));
    }

    [Fact]
    public void KnownBoundary_GetBfsDoesNotTraverseThroughNestedEdges()
    {
        // Documented limitation, not a bug: GetBfs (spec §6 M1, built before M2 existed) never
        // enqueues an edge-vertex as a traversal node -- it only uses GetVertexHyperedges/
        // GetHyperedgeVertices as a lookup *through* an already-visited domain vertex. So from
        // leaf0, GetBfs reaches leaf1 (edge1's other member) and then has nowhere to go: leaf1
        // is only a member of edge1, and edge1 itself is never "visited" to ask what edge2 (which
        // nests edge1) contains. Reaching leaf2/leaf3/leaf4 would require treating edge-vertices
        // as traversable nodes in their own right -- a real capability, but a different one than
        // M2's exit criterion asks for (storage round-trips, not traversal-through-reification),
        // and building it speculatively without a consumer forcing it would be exactly the
        // over-engineering this project's discipline exists to avoid. Locked in here so it's a
        // documented boundary, not a surprise discovered later.
        var kernel = new HypergraphKernel();
        var leaf0 = kernel.CreateVertex();
        var leaf1 = kernel.CreateVertex();
        var leaf2 = kernel.CreateVertex();
        var edge1 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge1, leaf0, 0, 0);
        kernel.AddIncidence(edge1, leaf1, 2, 1);
        var edge2 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge2, edge1, 0, 0);
        kernel.AddIncidence(edge2, leaf2, 2, 1);

        var reached = ((IHypergraphQuery)kernel).GetBfs(leaf0).ToList();

        Assert.Contains(leaf1, reached);
        Assert.DoesNotContain(leaf2, reached); // the boundary: unreachable via GetBfs today
    }
}
