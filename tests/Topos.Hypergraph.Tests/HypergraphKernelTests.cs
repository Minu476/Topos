using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

public class HypergraphKernelTests
{
    [Fact]
    public void CreateVertex_ReturnsResolvableHandle()
    {
        var kernel = new HypergraphKernel();
        var h = kernel.CreateVertex(VertexRoles.Edge);

        Assert.True(kernel.TryGetVertex(h, out var v));
        Assert.Equal(VertexRoles.Edge, v.Roles);
        Assert.Equal(VertexStatus.Active, v.Status);
    }

    [Fact]
    public void TryGetVertex_OnUnknownHandle_ReturnsFalse()
    {
        var kernel = new HypergraphKernel();
        Assert.False(kernel.TryGetVertex(new Handle(999), out _));
    }

    [Fact]
    public void TryGetVertex_OnUnknownHandle_OutVertexCarriesHandleInvalid()
    {
        // M8 decision (docs/DECISIONS.md): failure carries Handle.Invalid, not default(Handle)
        // (Index 0), so a caller that forgets to check the bool can't mistake this for real
        // vertex #0.
        var kernel = new HypergraphKernel();

        Assert.False(kernel.TryGetVertex(new Handle(999), out var v));
        Assert.Equal(Handle.Invalid, v.Handle);
        Assert.False(v.Handle.IsValid);
    }

    [Fact]
    public void NAryHyperedge_RoundTripsAllMembers_InOrdinalOrder()
    {
        // Mirrors RLB's HyperEdge shape (spec §1.1): one Anchor, N Conditions, one Target,
        // reified as an Edge-role vertex with role-tagged, ordinal-ordered incidences.
        var kernel = new HypergraphKernel();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        var anchor = kernel.CreateVertex();
        var condition1 = kernel.CreateVertex();
        var condition2 = kernel.CreateVertex();
        var target = kernel.CreateVertex();

        const byte anchorRole = 0, conditionRole = 1, targetRole = 2;
        kernel.AddIncidence(edge, anchor, anchorRole, ordinal: 0);
        kernel.AddIncidence(edge, condition1, conditionRole, ordinal: 1);
        kernel.AddIncidence(edge, condition2, conditionRole, ordinal: 2);
        kernel.AddIncidence(edge, target, targetRole, ordinal: 3);

        var members = kernel.IncidencesFrom(edge);
        Assert.Equal(4, members.Length);
        Assert.Equal([0, 1, 2, 3], members.Select(m => m.Ordinal).ToArray());
        Assert.Equal(1, members.Count(m => m.Role == anchorRole));
        Assert.Equal(2, members.Count(m => m.Role == conditionRole));
        Assert.Equal(1, members.Count(m => m.Role == targetRole));
    }

    [Fact]
    public void IncidencesOf_FindsReverseMembership()
    {
        var kernel = new HypergraphKernel();
        var edgeA = kernel.CreateVertex(VertexRoles.Edge);
        var edgeB = kernel.CreateVertex(VertexRoles.Edge);
        var sharedMember = kernel.CreateVertex();

        kernel.AddIncidence(edgeA, sharedMember, role: 1, ordinal: 0);
        kernel.AddIncidence(edgeB, sharedMember, role: 1, ordinal: 0);

        var memberships = kernel.IncidencesOf(sharedMember);
        Assert.Equal(2, memberships.Length);
        Assert.Contains(memberships, i => i.Source == edgeA);
        Assert.Contains(memberships, i => i.Source == edgeB);
    }

    [Fact]
    public void PropertyPool_RoundTripsTypedValues()
    {
        var kernel = new HypergraphKernel();
        var confidence = kernel.ResolveProperty<double>("confidence");
        var h = kernel.CreateVertex();

        kernel.SetProperty(confidence, h, 0.87);

        Assert.True(kernel.TryGetProperty(confidence, h, out var value));
        Assert.Equal(0.87, value);
    }

    [Fact]
    public void PropertyPool_UnsetProperty_ReturnsFalse()
    {
        var kernel = new HypergraphKernel();
        var confidence = kernel.ResolveProperty<double>("confidence");
        var h = kernel.CreateVertex();

        Assert.False(kernel.TryGetProperty(confidence, h, out _));
    }

    [Fact]
    public void ResolveProperty_SameNameTwice_ReturnsSameId()
    {
        var kernel = new HypergraphKernel();
        var a = kernel.ResolveProperty<int>("x");
        var b = kernel.ResolveProperty<int>("x");

        Assert.Equal(a.Id, b.Id);
    }
}
