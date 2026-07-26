namespace Topos.Hypergraph.Knowledge.Tests;

/// <summary>
/// Mirrors the RLB/NexusVerifier/ChatMemory shapes M9 generalizes: an Anchor/Condition/Target
/// hyperedge chain (`docs/DECISIONS.md`'s M9-SCOPED entry cites
/// `HypergraphKernelTests.NAryHyperedge_RoundTripsAllMembers_InOrdinalOrder` as the reference
/// shape). A Condition member is included in every edge specifically to prove role-gating
/// actually excludes it — a bug here would silently treat Conditions as reachable, which is
/// exactly what a topology-only traversal (<c>IHypergraphQuery.GetBfs</c>) would do and what M9
/// exists to avoid.
/// </summary>
public class DirectedTraversalTests
{
    private const byte AnchorRole = 0, ConditionRole = 1, TargetRole = 2;

    private static (HypergraphKernel Kernel, Handle A, Handle B, Handle C, Handle Condition1, Handle Condition2) BuildTwoHopChain()
    {
        var kernel = new HypergraphKernel();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var c = kernel.CreateVertex();
        var condition1 = kernel.CreateVertex();
        var condition2 = kernel.CreateVertex();

        var edge1 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge1, a, AnchorRole, 0);
        kernel.AddIncidence(edge1, condition1, ConditionRole, 1);
        kernel.AddIncidence(edge1, b, TargetRole, 2);

        var edge2 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge2, b, AnchorRole, 0);
        kernel.AddIncidence(edge2, condition2, ConditionRole, 1);
        kernel.AddIncidence(edge2, c, TargetRole, 2);

        return (kernel, a, b, c, condition1, condition2);
    }

    [Fact]
    public void DirectedBfs_FollowsOnlyAnchorToTargetLegs_AcrossMultipleHops()
    {
        var (kernel, a, b, c, _, _) = BuildTwoHopChain();
        IHypergraphQuery query = kernel;

        var reachable = query.DirectedBfs(a, AnchorRole, TargetRole);

        Assert.Equal([a, b, c], reachable);
    }

    [Fact]
    public void DirectedBfs_ExcludesConditionMembers_EvenThoughTheyShareTheSameHyperedges()
    {
        var (kernel, a, _, _, condition1, condition2) = BuildTwoHopChain();
        IHypergraphQuery query = kernel;

        var reachable = query.DirectedBfs(a, AnchorRole, TargetRole);

        Assert.DoesNotContain(condition1, reachable);
        Assert.DoesNotContain(condition2, reachable);
    }

    [Fact]
    public void DirectedBfs_ReturnsOnlyStart_WhenVertexHoldsNoAnchorRole()
    {
        var kernel = new HypergraphKernel();
        var isolated = kernel.CreateVertex();
        IHypergraphQuery query = kernel;

        var reachable = query.DirectedBfs(isolated, AnchorRole, TargetRole);

        Assert.Equal([isolated], reachable);
    }

    [Fact]
    public void DirectedShortestPath_ReconstructsFullPath_AcrossMultipleHops()
    {
        var (kernel, a, b, c, _, _) = BuildTwoHopChain();
        IHypergraphQuery query = kernel;

        var path = query.DirectedShortestPath(a, c, AnchorRole, TargetRole);

        Assert.Equal([a, b, c], path);
    }

    [Fact]
    public void DirectedShortestPath_ReturnsSingleElement_WhenFromEqualsTo()
    {
        var (kernel, a, _, _, _, _) = BuildTwoHopChain();
        IHypergraphQuery query = kernel;

        var path = query.DirectedShortestPath(a, a, AnchorRole, TargetRole);

        Assert.Equal([a], path);
    }

    [Fact]
    public void DirectedShortestPath_ReturnsEmpty_WhenUnreachable()
    {
        var (kernel, _, _, c, _, _) = BuildTwoHopChain();
        var unreachable = kernel.CreateVertex();
        IHypergraphQuery query = kernel;

        var path = query.DirectedShortestPath(c, unreachable, AnchorRole, TargetRole);

        Assert.Empty(path);
    }

    [Fact]
    public void RoleFilteredMembers_ReturnsOnlyMembersHoldingTheRequestedRole()
    {
        var kernel = new HypergraphKernel();
        var turn = kernel.CreateVertex();
        var mentioned1 = kernel.CreateVertex();
        var mentioned2 = kernel.CreateVertex();
        var speaker = kernel.CreateVertex();
        const byte speakerRole = 0, mentionedRole = 1;

        var edge = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge, turn, speakerRole, 0);
        kernel.AddIncidence(edge, speaker, speakerRole, 1);
        kernel.AddIncidence(edge, mentioned1, mentionedRole, 2);
        kernel.AddIncidence(edge, mentioned2, mentionedRole, 3);

        IHypergraphQuery query = kernel;
        var mentioned = query.RoleFilteredMembers(turn, mentionedRole);

        Assert.Equal([mentioned1, mentioned2], mentioned);
    }

    [Fact]
    public void RoleFilteredMembers_ReturnsEmpty_WhenVertexHasNoHyperedges()
    {
        var kernel = new HypergraphKernel();
        var isolated = kernel.CreateVertex();
        IHypergraphQuery query = kernel;

        Assert.Empty(query.RoleFilteredMembers(isolated, role: 0));
    }
}

/// <summary>Covers `docs/ROLE_CONVENTIONS.md`'s byte-backed-enum pattern as real code (M9's <see cref="RoleExtensions"/>).</summary>
public class RoleExtensionsTests
{
    private enum ChainerRole : byte
    {
        Before = 0,
        After = 1,
    }

    private enum WideRole : int
    {
        Before = 0,
        After = 1,
    }

    [Fact]
    public void AddIncidenceOfTRole_ByteBackedEnum_RoundTripsAsRawByte()
    {
        var kernel = new HypergraphKernel();
        var source = kernel.CreateVertex();
        var member = kernel.CreateVertex();

        var incidence = kernel.AddIncidence(source, member, ChainerRole.After, ordinal: 0);

        Assert.Equal((byte)ChainerRole.After, incidence.Role);
    }

    [Fact]
    public void AddIncidenceOfTRole_NonByteBackedEnum_ThrowsArgumentException()
    {
        var kernel = new HypergraphKernel();
        var source = kernel.CreateVertex();
        var member = kernel.CreateVertex();

        Assert.Throws<ArgumentException>(() => kernel.AddIncidence(source, member, WideRole.After, ordinal: 0));
    }

    [Fact]
    public void DirectedBfsOfTRole_TypedOverload_MatchesRawByteOverload()
    {
        var kernel = new HypergraphKernel();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge, a, ChainerRole.Before, 0);
        kernel.AddIncidence(edge, b, ChainerRole.After, 1);

        IHypergraphQuery query = kernel;
        var typed = query.DirectedBfs(a, ChainerRole.Before, ChainerRole.After);
        var raw = query.DirectedBfs(a, (byte)ChainerRole.Before, (byte)ChainerRole.After);

        Assert.Equal(raw, typed);
    }
}
