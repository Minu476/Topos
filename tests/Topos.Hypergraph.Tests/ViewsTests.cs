using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

/// <summary>M3: composable views (spec §6) — subgraph/mask (FilteredView) and union (UnionView).</summary>
public class ViewsTests
{
    [Fact]
    public void Subgraph_OnlyIncludesVerticesPassingPredicate()
    {
        var raw = new HypergraphKernel();
        var a = raw.CreateVertex();
        var b = raw.CreateVertex();
        var c = raw.CreateVertex();

        var view = HypergraphViews.Subgraph(raw, h => h == a || h == c);

        Assert.Equal(2, view.CountVertices());
        Assert.True(view.ContainsVertex(a));
        Assert.False(view.ContainsVertex(b));
        Assert.True(view.ContainsVertex(c));
    }

    [Fact]
    public void Subgraph_EdgeExcluded_WhenEdgeVertexItselfFailsPredicate()
    {
        var raw = new HypergraphKernel();
        var edge = raw.CreateVertex(VertexRoles.Edge);
        var member = raw.CreateVertex();
        raw.AddIncidence(edge, member, 0, 0);

        var view = HypergraphViews.Subgraph(raw, h => h != edge); // excludes the edge itself

        Assert.Empty(view.GetHyperedgeVertices(edge)); // edge fails predicate, so no members reported
    }

    [Fact]
    public void Subgraph_MemberOutsideView_SilentlyDroppedFromEdgeMembership()
    {
        // Mirrors JGraphT's AsSubgraph convention: an edge with one member inside the view and
        // one outside reports only the in-view member, not an error.
        var raw = new HypergraphKernel();
        var edge = raw.CreateVertex(VertexRoles.Edge);
        var inside = raw.CreateVertex();
        var outside = raw.CreateVertex();
        raw.AddIncidence(edge, inside, 0, 0);
        raw.AddIncidence(edge, outside, 2, 1);

        var view = HypergraphViews.Subgraph(raw, h => h == edge || h == inside);

        var members = view.GetHyperedgeVertices(edge);
        Assert.Single(members);
        Assert.Equal(inside, members[0].Member);
    }

    [Fact]
    public void Subgraph_AlgorithmsWorkOverTheView_ForFree()
    {
        // GetBfs is default-implemented purely from the 5 primitives -- it needs zero extra code
        // to work correctly over a FilteredView. Traversal must not "escape" the view boundary.
        var raw = new HypergraphKernel();
        var a = raw.CreateVertex();
        var b = raw.CreateVertex();
        var c = raw.CreateVertex(); // outside the view
        var e1 = raw.CreateVertex(VertexRoles.Edge);
        var e2 = raw.CreateVertex(VertexRoles.Edge);
        raw.AddIncidence(e1, a, 0, 0);
        raw.AddIncidence(e1, b, 2, 1);
        raw.AddIncidence(e2, b, 0, 0);
        raw.AddIncidence(e2, c, 2, 1);

        var view = HypergraphViews.Subgraph(raw, h => h == a || h == b || h == e1 || h == e2);
        var reached = view.GetBfs(a).ToList();

        Assert.Contains(a, reached);
        Assert.Contains(b, reached);
        Assert.DoesNotContain(c, reached); // c is outside the view, even though e2 (in the view) points to it
    }

    [Fact]
    public void Mask_IsTheSameMechanismAsSubgraph()
    {
        var raw = new HypergraphKernel();
        var a = raw.CreateVertex();
        var b = raw.CreateVertex();

        var subgraph = HypergraphViews.Subgraph(raw, h => h == a);
        var mask = HypergraphViews.Mask(raw, h => h == a);

        Assert.Equal(subgraph.VertexHandles(), mask.VertexHandles());
    }

    [Fact]
    public void Union_CombinesVerticesFromBothViews()
    {
        var raw = new HypergraphKernel();
        var a = raw.CreateVertex();
        var b = raw.CreateVertex();
        var c = raw.CreateVertex();

        var viewA = HypergraphViews.Subgraph(raw, h => h == a);
        var viewB = HypergraphViews.Subgraph(raw, h => h == b || h == c);
        var union = HypergraphViews.Union(viewA, viewB);

        Assert.Equal(3, union.CountVertices());
        Assert.True(union.ContainsVertex(a));
        Assert.True(union.ContainsVertex(b));
        Assert.True(union.ContainsVertex(c));
    }

    [Fact]
    public void Union_OnConflict_APrefersItsOwnVertexData()
    {
        var raw = new HypergraphKernel();
        var shared = raw.CreateVertex(VertexRoles.Edge);

        // Two views disagreeing about the same Handle's data -- an artificial but valid scenario
        // to pin down the documented "a wins" rule. FilteredView delegates TryGetVertex to the
        // real underlying kernel, so to actually get different Vertex data for the same Handle
        // we'd need two different kernels -- here we just confirm the precedence order directly
        // via two sources that both resolve the handle, using UnionView's own short-circuit.
        var a = new AlwaysResolves(shared, new Vertex(shared, VertexRoles.Edge, VertexStatus.Active));
        var b = new AlwaysResolves(shared, new Vertex(shared, VertexRoles.None, VertexStatus.Dormant));

        var union = HypergraphViews.Union(a, b);

        Assert.True(union.TryGetVertex(shared, out var resolved));
        Assert.Equal(VertexRoles.Edge, resolved.Roles); // a's version, not b's
    }

    /// <summary>Minimal IHypergraphQuery stub that always resolves one fixed Handle -- used only to pin down UnionView's conflict precedence in isolation.</summary>
    private sealed class AlwaysResolves(Handle handle, Vertex vertex) : IHypergraphQuery
    {
        public int CountVertices() => 1;
        public IReadOnlyList<Handle> VertexHandles() => [handle];
        public bool TryGetVertex(Handle h, out Vertex v)
        {
            if (h == handle) { v = vertex; return true; }
            v = default; return false;
        }
        public IReadOnlyList<Handle> GetVertexHyperedges(Handle h) => [];
        public IReadOnlyList<Incidence> GetHyperedgeVertices(Handle h) => [];
    }
}
