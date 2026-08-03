namespace Topos.Hypergraph.Knowledge.Tests;

/// <summary>
/// <see cref="DirectedTraversal.DirectedScc"/> — spec §5.4's must-have. Component *grouping* is
/// the cross-implementation invariant (which vertices share a component), not the outer list's
/// order or the community's numeric position, matching the same convention
/// <c>AnalyticsGdsParityTests.LabelPropagation_AgreesWithGdsOnWhichVerticesShareACommunity</c> uses
/// in <c>Topos.Tests.GdsOracle</c>.
/// </summary>
public class DirectedSccTests
{
    private const byte AnchorRole = 0, ConditionRole = 1, TargetRole = 2;

    private static IReadOnlyList<Handle>? ComponentOf(IReadOnlyList<IReadOnlyList<Handle>> components, Handle v) =>
        components.FirstOrDefault(c => c.Contains(v));

    [Fact]
    public void ThreeCycle_OfBinaryAnchorTargetLegs_IsOneComponent()
    {
        // Reduction-anchor shape (spec §4.5 T5): every hyperedge here is binary (Anchor+Target
        // only, no Condition) -- a->b->c->a. Three distinct 2-member hyperedges, each contributing
        // one directed leg to the cycle.
        var kernel = new HypergraphKernel();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var c = kernel.CreateVertex();

        var edge1 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge1, a, AnchorRole, 0);
        kernel.AddIncidence(edge1, b, TargetRole, 1);
        var edge2 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge2, b, AnchorRole, 0);
        kernel.AddIncidence(edge2, c, TargetRole, 1);
        var edge3 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge3, c, AnchorRole, 0);
        kernel.AddIncidence(edge3, a, TargetRole, 1);

        IHypergraphQuery query = kernel;
        var components = query.DirectedScc(AnchorRole, TargetRole);

        var componentOfA = ComponentOf(components, a);
        Assert.NotNull(componentOfA);
        Assert.Equal(3, componentOfA!.Count);
        Assert.Contains(a, componentOfA);
        Assert.Contains(b, componentOfA);
        Assert.Contains(c, componentOfA);
    }

    [Fact]
    public void AcyclicChain_OfAnchorTargetLegs_IsThreeSingletonComponents()
    {
        var kernel = new HypergraphKernel();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var c = kernel.CreateVertex();

        var edge1 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge1, a, AnchorRole, 0);
        kernel.AddIncidence(edge1, b, TargetRole, 1);
        var edge2 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge2, b, AnchorRole, 0);
        kernel.AddIncidence(edge2, c, TargetRole, 1);

        IHypergraphQuery query = kernel;
        var components = query.DirectedScc(AnchorRole, TargetRole);

        Assert.NotEqual(ComponentOf(components, a), ComponentOf(components, b));
        Assert.NotEqual(ComponentOf(components, b), ComponentOf(components, c));
        Assert.Single(ComponentOf(components, a)!);
        Assert.Single(ComponentOf(components, b)!);
        Assert.Single(ComponentOf(components, c)!);
    }

    [Fact]
    public void ConditionMembers_NeverJoinTheCycleComponent_EvenWhenCoIncidentOnEveryEdge()
    {
        // Same n-ary shape as DirectedTraversalTests.BuildTwoHopChain, but closed into a cycle:
        // a --edge1(Condition1)--> b --edge2(Condition2)--> a. A bug that treated Condition as
        // Anchor/Target-equivalent would fold condition1/condition2 into the {a,b} component too.
        var kernel = new HypergraphKernel();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var condition1 = kernel.CreateVertex();
        var condition2 = kernel.CreateVertex();

        var edge1 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge1, a, AnchorRole, 0);
        kernel.AddIncidence(edge1, condition1, ConditionRole, 1);
        kernel.AddIncidence(edge1, b, TargetRole, 2);

        var edge2 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge2, b, AnchorRole, 0);
        kernel.AddIncidence(edge2, condition2, ConditionRole, 1);
        kernel.AddIncidence(edge2, a, TargetRole, 2);

        IHypergraphQuery query = kernel;
        var components = query.DirectedScc(AnchorRole, TargetRole);

        var cycleComponent = ComponentOf(components, a);
        Assert.Equal(2, cycleComponent!.Count);
        Assert.Contains(a, cycleComponent);
        Assert.Contains(b, cycleComponent);
        Assert.DoesNotContain(condition1, cycleComponent);
        Assert.DoesNotContain(condition2, cycleComponent);
        Assert.Single(ComponentOf(components, condition1)!);
        Assert.Single(ComponentOf(components, condition2)!);
    }

    [Fact]
    public void EveryVertex_IsInExactlyOneComponent()
    {
        // Partition invariant (gap list §4.1, property #10).
        var kernel = new HypergraphKernel();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge, a, AnchorRole, 0);
        kernel.AddIncidence(edge, b, TargetRole, 1);

        IHypergraphQuery query = kernel;
        var components = query.DirectedScc(AnchorRole, TargetRole);

        var allMembers = components.SelectMany(c => c).ToList();
        var allVertices = kernel.VertexHandles();

        Assert.Equal(allVertices.Count, allMembers.Count); // no duplicates across components
        Assert.Equal(allVertices.OrderBy(h => h.Index), allMembers.OrderBy(h => h.Index));
    }

    [Fact]
    public void EmptyGraph_ReturnsNoComponents()
    {
        var kernel = new HypergraphKernel();
        IHypergraphQuery query = kernel;

        Assert.Empty(query.DirectedScc(AnchorRole, TargetRole));
    }

    [Fact]
    public void DirectedSccOfTRole_TypedOverload_MatchesRawByteOverload()
    {
        var kernel = new HypergraphKernel();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var edge1 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge1, a, AnchorRole, 0);
        kernel.AddIncidence(edge1, b, TargetRole, 1);
        var edge2 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge2, b, AnchorRole, 0);
        kernel.AddIncidence(edge2, a, TargetRole, 1);

        IHypergraphQuery query = kernel;
        var typed = query.DirectedScc(ChainerRole.Anchor, ChainerRole.Target);
        var raw = query.DirectedScc((byte)ChainerRole.Anchor, (byte)ChainerRole.Target);

        Assert.Equal(
            raw.Select(c => c.OrderBy(h => h.Index).ToList()).OrderBy(c => c[0].Index),
            typed.Select(c => c.OrderBy(h => h.Index).ToList()).OrderBy(c => c[0].Index));
    }

    private enum ChainerRole : byte
    {
        Anchor = 0,
        Target = 2,
    }
}
