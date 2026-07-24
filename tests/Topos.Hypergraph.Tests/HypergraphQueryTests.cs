using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

/// <summary>Tests for IHypergraphQuery's required primitives and its default-implemented derivations/algorithms.</summary>
public class HypergraphQueryTests
{
    [Fact]
    public void CountVertices_ReflectsAllCreatedVertices_IncludingDormant()
    {
        IHypergraphQuery kernel = new HypergraphKernel();
        var a = ((HypergraphKernel)kernel).CreateVertex();
        var b = ((HypergraphKernel)kernel).CreateVertex();
        ((HypergraphKernel)kernel).SetDormant(a);

        Assert.Equal(2, kernel.CountVertices());
    }

    [Fact]
    public void IsEmpty_TrueBeforeAnyVertex_FalseAfter()
    {
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;

        Assert.True(kernel.IsEmpty());
        raw.CreateVertex();
        Assert.False(kernel.IsEmpty());
    }

    [Fact]
    public void VertexHandles_ReturnsEverySnapshot()
    {
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var handles = new[] { raw.CreateVertex(), raw.CreateVertex(), raw.CreateVertex() };

        Assert.Equal(handles.Length, kernel.VertexHandles().Count);
        foreach (var h in handles) Assert.Contains(h, kernel.VertexHandles());
    }

    [Fact]
    public void ContainsVertex_TrueForKnown_FalseForUnknown()
    {
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var h = raw.CreateVertex();

        Assert.True(kernel.ContainsVertex(h));
        Assert.False(kernel.ContainsVertex(new Handle(9999)));
    }

    [Fact]
    public void GetVertex_ThrowsOnUnknownHandle()
    {
        IHypergraphQuery kernel = new HypergraphKernel();
        Assert.Throws<KeyNotFoundException>(() => kernel.GetVertex(new Handle(1234)));
    }

    [Fact]
    public void HyperedgeHandles_FindsOnlyEdgeTaggedVertices()
    {
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var edge = raw.CreateVertex(VertexRoles.Edge);
        var plain1 = raw.CreateVertex();
        var plain2 = raw.CreateVertex();

        var edges = kernel.HyperedgeHandles();
        Assert.Single(edges);
        Assert.Equal(edge, edges[0]);
        Assert.Equal(1, kernel.CountHyperedges());
    }

    [Fact]
    public void GetVertexHyperedges_And_GetHyperedgeVertices_AreConsistent()
    {
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var edge = raw.CreateVertex(VertexRoles.Edge);
        var anchor = raw.CreateVertex();
        var target = raw.CreateVertex();

        raw.AddIncidence(edge, anchor, role: 0, ordinal: 0);
        raw.AddIncidence(edge, target, role: 2, ordinal: 1);

        var edgesOfAnchor = kernel.GetVertexHyperedges(anchor);
        Assert.Single(edgesOfAnchor);
        Assert.Equal(edge, edgesOfAnchor[0]);

        var members = kernel.GetHyperedgeVertices(edge);
        Assert.Equal(2, members.Count);
        Assert.Contains(members, m => m.Member == anchor && m.Role == 0);
        Assert.Contains(members, m => m.Member == target && m.Role == 2);
    }

    [Fact]
    public void GetBfs_WalksAcrossSharedHyperedgeMembership()
    {
        // a -- edge1 --> b -- edge2 --> c   (a chain via two reified hyperedges)
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var a = raw.CreateVertex();
        var b = raw.CreateVertex();
        var c = raw.CreateVertex();
        var edge1 = raw.CreateVertex(VertexRoles.Edge);
        var edge2 = raw.CreateVertex(VertexRoles.Edge);

        raw.AddIncidence(edge1, a, role: 0, ordinal: 0);
        raw.AddIncidence(edge1, b, role: 2, ordinal: 1);
        raw.AddIncidence(edge2, b, role: 0, ordinal: 0);
        raw.AddIncidence(edge2, c, role: 2, ordinal: 1);

        var reached = kernel.GetBfs(a).ToList();

        Assert.Contains(a, reached);
        Assert.Contains(b, reached);
        Assert.Contains(c, reached);
        Assert.True(kernel.IsReachable(a, c));
    }

    [Fact]
    public void GetBfs_FromUnknownHandle_YieldsNothing()
    {
        IHypergraphQuery kernel = new HypergraphKernel();
        Assert.Empty(kernel.GetBfs(new Handle(777)));
    }

    [Fact]
    public void IsReachable_FalseForDisconnectedVertices()
    {
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var a = raw.CreateVertex();
        var isolated = raw.CreateVertex();

        Assert.False(kernel.IsReachable(a, isolated));
    }

    [Fact]
    public void GetBfs_DoesNotRevisitVertices_OnCyclicHyperedges()
    {
        // a <-> b via one hyperedge with both as members (a cycle back to itself through re-visit
        // potential) -- BFS must terminate and not loop forever or double-yield.
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var a = raw.CreateVertex();
        var b = raw.CreateVertex();
        var edge = raw.CreateVertex(VertexRoles.Edge);

        raw.AddIncidence(edge, a, role: 0, ordinal: 0);
        raw.AddIncidence(edge, b, role: 2, ordinal: 1);
        // A second edge linking b back to a, forming a cycle.
        var edge2 = raw.CreateVertex(VertexRoles.Edge);
        raw.AddIncidence(edge2, b, role: 0, ordinal: 0);
        raw.AddIncidence(edge2, a, role: 2, ordinal: 1);

        var reached = kernel.GetBfs(a).ToList();

        Assert.Equal(2, reached.Count); // a, b -- each visited exactly once despite the cycle
        Assert.Equal(reached.Count, reached.Distinct().Count());
    }
}
