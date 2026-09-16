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

    [Fact]
    public void NoIncidenceRemovalExists()
    {
        // Invariant 3, pinned by absence. The kernel offers no way to withdraw a membership --
        // AddIncidence has no counterpart -- and that is a decision (docs/DECISIONS.md 2026-09-16,
        // docs/CONCEPTS.md "Invariant 3"), not an omission waiting to be filled in. Asserting it
        // here means adding one trips a failing test and forces the decision to be revisited
        // deliberately, rather than the invariant eroding in a routine PR.
        var removalLike = typeof(HypergraphKernel)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => m.Name.Contains("Incidence", StringComparison.Ordinal))
            .Where(m => m.Name.StartsWith("Remove", StringComparison.Ordinal)
                     || m.Name.StartsWith("Delete", StringComparison.Ordinal)
                     || m.Name.StartsWith("Detach", StringComparison.Ordinal)
                     || m.Name.StartsWith("Clear", StringComparison.Ordinal))
            .Select(m => m.Name)
            .ToList();

        Assert.Empty(removalLike);
    }

    [Fact]
    public void Membership_SurvivesDormancyOfEveryParticipant()
    {
        // The operational consequence: superseding a relation (tombstone the old hyperedge, add a
        // corrected one) never destroys the original membership -- it stays readable from both
        // directions, which is what makes the supersede idiom a record rather than a deletion.
        var kernel = new HypergraphKernel();
        var member = kernel.CreateVertex();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge, member, role: 3, ordinal: 0);

        kernel.SetDormant(edge);
        kernel.SetDormant(member);

        Assert.Single(kernel.IncidencesFrom(edge));
        Assert.Equal(member, kernel.IncidencesFrom(edge)[0].Member);
        Assert.Single(kernel.IncidencesOf(member));
    }
}
