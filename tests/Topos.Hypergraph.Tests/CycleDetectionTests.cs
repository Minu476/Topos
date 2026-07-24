using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

public class CycleDetectionTests
{
    [Fact]
    public void HasCycle_EmptyGraph_IsFalse()
    {
        IHypergraphQuery kernel = new HypergraphKernel();
        Assert.False(kernel.HasCycle());
    }

    [Fact]
    public void HasCycle_SingleVertex_IsFalse()
    {
        var raw = new HypergraphKernel();
        raw.CreateVertex();
        Assert.False(((IHypergraphQuery)raw).HasCycle());
    }

    [Fact]
    public void HasCycle_BinaryChain_IsFalse()
    {
        // a --e1--> b --e2--> c, each hyperedge exactly 2 members: a genuine acyclic chain.
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var a = raw.CreateVertex();
        var b = raw.CreateVertex();
        var c = raw.CreateVertex();
        var e1 = raw.CreateVertex(VertexRoles.Edge);
        var e2 = raw.CreateVertex(VertexRoles.Edge);
        raw.AddIncidence(e1, a, 0, 0); raw.AddIncidence(e1, b, 2, 1);
        raw.AddIncidence(e2, b, 0, 0); raw.AddIncidence(e2, c, 2, 1);

        Assert.False(kernel.HasCycle());
    }

    [Fact]
    public void HasCycle_BinaryTriangle_IsTrue()
    {
        // a-b, b-c, c-a via three separate 2-member hyperedges: a genuine triangle.
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var a = raw.CreateVertex();
        var b = raw.CreateVertex();
        var c = raw.CreateVertex();
        var eAB = raw.CreateVertex(VertexRoles.Edge);
        var eBC = raw.CreateVertex(VertexRoles.Edge);
        var eCA = raw.CreateVertex(VertexRoles.Edge);
        raw.AddIncidence(eAB, a, 0, 0); raw.AddIncidence(eAB, b, 2, 1);
        raw.AddIncidence(eBC, b, 0, 0); raw.AddIncidence(eBC, c, 2, 1);
        raw.AddIncidence(eCA, c, 0, 0); raw.AddIncidence(eCA, a, 2, 1);

        Assert.True(kernel.HasCycle());
    }

    [Fact]
    public void HasCycle_ThreeMemberHyperedge_IsTriviallyTrue()
    {
        // Documented consequence (see IHypergraphQuery.HasCycle's doc comment): a single
        // hyperedge with 3+ members is a clique under the topology-only reading, and any clique
        // of size >= 3 contains a triangle. This is RLB's actual shape (Anchor + Condition(s) +
        // Target), so this test locks in the documented, deliberate behavior.
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var edge = raw.CreateVertex(VertexRoles.Edge);
        var anchor = raw.CreateVertex();
        var condition = raw.CreateVertex();
        var target = raw.CreateVertex();
        raw.AddIncidence(edge, anchor, 0, 0);
        raw.AddIncidence(edge, condition, 1, 1);
        raw.AddIncidence(edge, target, 2, 2);

        Assert.True(kernel.HasCycle());
    }

    [Fact]
    public void HasCycle_TwoMemberHyperedge_IsFalse()
    {
        // The degenerate D2 case with zero Conditions (Anchor + Target only) is a plain binary
        // edge -- not trivially cyclic.
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var edge = raw.CreateVertex(VertexRoles.Edge);
        var anchor = raw.CreateVertex();
        var target = raw.CreateVertex();
        raw.AddIncidence(edge, anchor, 0, 0);
        raw.AddIncidence(edge, target, 2, 1);

        Assert.False(kernel.HasCycle());
    }

    [Fact]
    public void HasCycle_ChecksAllComponents_NotJustTheFirst()
    {
        // A disconnected acyclic pair PLUS a separate disconnected triangle -- must still report
        // true even though the first component scanned (by handle order) is acyclic.
        var raw = new HypergraphKernel();
        IHypergraphQuery kernel = raw;
        var a = raw.CreateVertex();
        var b = raw.CreateVertex();
        var eAB = raw.CreateVertex(VertexRoles.Edge);
        raw.AddIncidence(eAB, a, 0, 0); raw.AddIncidence(eAB, b, 2, 1);

        var x = raw.CreateVertex();
        var y = raw.CreateVertex();
        var z = raw.CreateVertex();
        var eXY = raw.CreateVertex(VertexRoles.Edge);
        var eYZ = raw.CreateVertex(VertexRoles.Edge);
        var eZX = raw.CreateVertex(VertexRoles.Edge);
        raw.AddIncidence(eXY, x, 0, 0); raw.AddIncidence(eXY, y, 2, 1);
        raw.AddIncidence(eYZ, y, 0, 0); raw.AddIncidence(eYZ, z, 2, 1);
        raw.AddIncidence(eZX, z, 0, 0); raw.AddIncidence(eZX, x, 2, 1);

        Assert.True(kernel.HasCycle());
    }
}
