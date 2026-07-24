using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

/// <summary>Regression tests for the two spec §3 invariants.</summary>
public class InvariantTests
{
    [Fact]
    public void Invariant1_DormantVertexRemainsResolvable()
    {
        var kernel = new HypergraphKernel();
        var h = kernel.CreateVertex();

        kernel.SetDormant(h);

        Assert.True(kernel.TryGetVertex(h, out var v));
        Assert.True(v.IsDormant);
    }

    [Fact]
    public void Invariant1_ProvenanceEdgeResolvesToDormantTarget()
    {
        var kernel = new HypergraphKernel();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        var target = kernel.CreateVertex();

        kernel.AddIncidence(edge, target, role: 0, ordinal: 0);
        kernel.SetDormant(target);

        var incidences = kernel.IncidencesFrom(edge);
        Assert.Single(incidences);
        Assert.True(kernel.TryGetVertex(incidences[0].Member, out var resolved));
        Assert.True(resolved.IsDormant);
    }

    [Fact]
    public void Invariant1_ReactivateUndoesDormancy()
    {
        var kernel = new HypergraphKernel();
        var h = kernel.CreateVertex();

        kernel.SetDormant(h);
        kernel.Reactivate(h);

        Assert.True(kernel.TryGetVertex(h, out var v));
        Assert.False(v.IsDormant);
    }

    [Fact]
    public void Invariant2_VertexRolesAndIncidenceRoleAreIndependentAxes()
    {
        var kernel = new HypergraphKernel();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        var member = kernel.CreateVertex(VertexRoles.None);

        // IncidenceRole (a raw byte, domain-defined) varies freely regardless of VertexRoles —
        // the two are unrelated fields on unrelated types, not derived from one another.
        kernel.AddIncidence(edge, member, role: 7, ordinal: 0);

        Assert.True(kernel.TryGetVertex(edge, out var edgeVertex));
        Assert.True(kernel.TryGetVertex(member, out var memberVertex));
        Assert.Equal(VertexRoles.Edge, edgeVertex.Roles);
        Assert.Equal(VertexRoles.None, memberVertex.Roles);
        Assert.Equal((byte)7, kernel.IncidencesFrom(edge)[0].Role);
    }

    [Fact]
    public void HandleIdentity_IsStableAcrossDormancyTransitions()
    {
        var kernel = new HypergraphKernel();
        var h = kernel.CreateVertex();

        kernel.SetDormant(h);
        kernel.Reactivate(h);
        kernel.SetDormant(h);

        // The Handle itself never changes — only Status does. This is the "never GC'd" half of
        // Invariant 1: there is no removal-then-recreate path that could mint a new Index.
        Assert.True(kernel.TryGetVertex(h, out var v));
        Assert.Equal(h, v.Handle);
    }
}
