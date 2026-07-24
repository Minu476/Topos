using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

public class DfsTests
{
    [Fact]
    public void GetDfs_ReachesEverythingBfsReaches_OnAChain()
    {
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

        var dfs = kernel.GetDfs(a).OrderBy(h => h.Index).ToList();
        var bfs = kernel.GetBfs(a).OrderBy(h => h.Index).ToList();

        Assert.Equal(bfs, dfs);
    }

    [Fact]
    public void GetDfs_FromUnknownHandle_YieldsNothing()
    {
        IHypergraphQuery kernel = new HypergraphKernel();
        Assert.Empty(kernel.GetDfs(new Handle(555)));
    }

    [Fact]
    public void GetDfs_VisitsEachVertexExactlyOnce_OnCyclicGraph()
    {
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var a = raw.CreateVertex();
        var b = raw.CreateVertex();
        var c = raw.CreateVertex();
        var edge = raw.CreateVertex(VertexRoles.Edge);
        raw.AddIncidence(edge, a, role: 0, ordinal: 0);
        raw.AddIncidence(edge, b, role: 1, ordinal: 1);
        raw.AddIncidence(edge, c, role: 2, ordinal: 2);

        var dfs = kernel.GetDfs(a).ToList();

        Assert.Equal(3, dfs.Count);
        Assert.Equal(dfs.Count, dfs.Distinct().Count());
    }

    [Fact]
    public void GetDfs_SingleVertex_YieldsJustItself()
    {
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var a = raw.CreateVertex();

        Assert.Equal([a], kernel.GetDfs(a));
    }
}
