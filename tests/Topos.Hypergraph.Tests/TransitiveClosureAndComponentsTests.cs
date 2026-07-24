using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

public class TransitiveClosureAndComponentsTests
{
    [Fact]
    public void GetTransitiveClosure_AgreesWithIsReachable_ForEveryPair()
    {
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var a = raw.CreateVertex();
        var b = raw.CreateVertex();
        var c = raw.CreateVertex();
        var isolated = raw.CreateVertex();
        var e1 = raw.CreateVertex(VertexRoles.Edge);
        var e2 = raw.CreateVertex(VertexRoles.Edge);
        raw.AddIncidence(e1, a, 0, 0); raw.AddIncidence(e1, b, 2, 1);
        raw.AddIncidence(e2, b, 0, 0); raw.AddIncidence(e2, c, 2, 1);

        var closure = kernel.GetTransitiveClosure();
        var allVertices = kernel.VertexHandles();

        foreach (var from in allVertices)
        {
            foreach (var to in allVertices)
            {
                if (from == to) continue;
                bool inClosure = closure[from].Contains(to);
                bool reachable = kernel.IsReachable(from, to);
                Assert.Equal(reachable, inClosure);
            }
        }

        Assert.DoesNotContain(isolated, closure[a]);
    }

    [Fact]
    public void GetTransitiveClosure_ExcludesSelf()
    {
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var a = raw.CreateVertex();

        Assert.DoesNotContain(a, kernel.GetTransitiveClosure()[a]);
    }

    [Fact]
    public void GetConnectedComponents_GroupsConnectedVertices_SeparatesDisconnected()
    {
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var a = raw.CreateVertex();
        var b = raw.CreateVertex();
        var edge = raw.CreateVertex(VertexRoles.Edge);
        raw.AddIncidence(edge, a, 0, 0);
        raw.AddIncidence(edge, b, 2, 1);

        var isolated1 = raw.CreateVertex();
        var isolated2 = raw.CreateVertex();

        var components = kernel.GetConnectedComponents();

        Assert.Equal(3, components.Count); // {a,b,edge}, {isolated1}, {isolated2}
        Assert.Contains(components, c => c.Count == 3 && c.Contains(a) && c.Contains(b) && c.Contains(edge));
        Assert.Contains(components, c => c.Count == 1 && c.Contains(isolated1));
        Assert.Contains(components, c => c.Count == 1 && c.Contains(isolated2));
    }

    [Fact]
    public void GetConnectedComponents_EmptyGraph_ReturnsNoComponents()
    {
        IHypergraphQuery kernel = new HypergraphKernel();
        Assert.Empty(kernel.GetConnectedComponents());
    }

    [Fact]
    public void GetConnectedComponents_PartitionsEveryVertexExactlyOnce()
    {
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        for (int i = 0; i < 10; i++) raw.CreateVertex();

        var components = kernel.GetConnectedComponents();
        var allInComponents = components.SelectMany(c => c).ToList();

        Assert.Equal(kernel.CountVertices(), allInComponents.Count);
        Assert.Equal(allInComponents.Count, allInComponents.Distinct().Count());
    }
}
