namespace Topos.Hypergraph.Living.Tests;

/// <summary>
/// Tests for <see cref="ObservationChain"/> — the timestamped observation-chain / state-history
/// convenience (docs/DECISIONS.md's M12 entry). Role bytes are plain constants here, exactly the
/// "no library-shipped role enum" convention <see cref="ObservationChain"/> itself documents.
/// </summary>
public class ObservationChainTests
{
    private const byte EntityRole = 0, PreviousRole = 1;
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RecordObservation_WithNoPrevious_CreatesEdgeVertexIncidentOnlyToEntity()
    {
        var kernel = new HypergraphKernel();
        var entity = kernel.CreateVertex();

        var observation = kernel.RecordObservation(entity, EntityRole, previous: null, PreviousRole, T0);

        var incidences = kernel.GetHyperedgeVertices(observation);
        var incidence = Assert.Single(incidences);
        Assert.Equal(EntityRole, incidence.Role);
        Assert.Equal(entity, incidence.Member);
    }

    [Fact]
    public void RecordObservation_WithPrevious_LinksBothEntityAndPreviousObservation()
    {
        var kernel = new HypergraphKernel();
        var entity = kernel.CreateVertex();
        var first = kernel.RecordObservation(entity, EntityRole, previous: null, PreviousRole, T0);

        var second = kernel.RecordObservation(entity, EntityRole, first, PreviousRole, T0.AddMinutes(1));

        var incidences = kernel.GetHyperedgeVertices(second);
        Assert.Equal(2, incidences.Count);
        Assert.Contains(incidences, i => i.Role == EntityRole && i.Member == entity && i.Ordinal == 0);
        Assert.Contains(incidences, i => i.Role == PreviousRole && i.Member == first && i.Ordinal == 1);
    }

    [Fact]
    public void RecordObservation_StampsRecordedAt_ExactValueRoundTrips()
    {
        var kernel = new HypergraphKernel();
        var entity = kernel.CreateVertex();
        var when = new DateTimeOffset(2026, 3, 14, 9, 26, 53, TimeSpan.FromHours(-5));

        var observation = kernel.RecordObservation(entity, EntityRole, null, PreviousRole, when);

        Assert.Equal(when, kernel.RecordedAt(observation));
    }

    [Fact]
    public void RecordedAt_ReturnsNull_ForVertexNeverRecorded()
    {
        var kernel = new HypergraphKernel();
        var plainVertex = kernel.CreateVertex();

        Assert.Null(kernel.RecordedAt(plainVertex));
    }

    [Fact]
    public void ObservationHistory_ChainOfThree_WalksNewestToOldestInclusive()
    {
        var kernel = new HypergraphKernel();
        var entity = kernel.CreateVertex();
        var obs1 = kernel.RecordObservation(entity, EntityRole, null, PreviousRole, T0);
        var obs2 = kernel.RecordObservation(entity, EntityRole, obs1, PreviousRole, T0.AddMinutes(1));
        var obs3 = kernel.RecordObservation(entity, EntityRole, obs2, PreviousRole, T0.AddMinutes(2));
        IHypergraphQuery query = kernel;

        var history = query.ObservationHistory(obs3, PreviousRole).ToList();

        Assert.Equal([obs3, obs2, obs1], history);
    }

    [Fact]
    public void ObservationHistory_SingleObservation_YieldsOnlyItself()
    {
        var kernel = new HypergraphKernel();
        var entity = kernel.CreateVertex();
        var obs1 = kernel.RecordObservation(entity, EntityRole, null, PreviousRole, T0);
        IHypergraphQuery query = kernel;

        var history = query.ObservationHistory(obs1, PreviousRole).ToList();

        Assert.Equal([obs1], history);
    }

    [Fact]
    public void PreviousObservation_ReturnsNull_AtGenesis()
    {
        var kernel = new HypergraphKernel();
        var entity = kernel.CreateVertex();
        var obs1 = kernel.RecordObservation(entity, EntityRole, null, PreviousRole, T0);
        IHypergraphQuery query = kernel;

        Assert.Null(query.PreviousObservation(obs1, PreviousRole));
    }

    [Fact]
    public void EntityOfObservation_ReturnsNull_ForVertexNeverUsedAsObservation()
    {
        var kernel = new HypergraphKernel();
        var plainVertex = kernel.CreateVertex();
        IHypergraphQuery query = kernel;

        Assert.Null(query.EntityOfObservation(plainVertex, EntityRole));
    }

    [Fact]
    public void EntityOfObservation_ReturnsEntity_ForRealObservation()
    {
        var kernel = new HypergraphKernel();
        var entity = kernel.CreateVertex();
        var obs1 = kernel.RecordObservation(entity, EntityRole, null, PreviousRole, T0);
        IHypergraphQuery query = kernel;

        Assert.Equal(entity, query.EntityOfObservation(obs1, EntityRole));
    }

    [Fact]
    public void TwoEntities_IndependentChains_DoNotCrossContaminate()
    {
        var kernel = new HypergraphKernel();
        var entityA = kernel.CreateVertex();
        var entityB = kernel.CreateVertex();

        var aObs1 = kernel.RecordObservation(entityA, EntityRole, null, PreviousRole, T0);
        var aObs2 = kernel.RecordObservation(entityA, EntityRole, aObs1, PreviousRole, T0.AddMinutes(1));

        var bObs1 = kernel.RecordObservation(entityB, EntityRole, null, PreviousRole, T0);
        var bObs2 = kernel.RecordObservation(entityB, EntityRole, bObs1, PreviousRole, T0.AddMinutes(1));

        IHypergraphQuery query = kernel;
        var aHistory = query.ObservationHistory(aObs2, PreviousRole).ToList();
        var bHistory = query.ObservationHistory(bObs2, PreviousRole).ToList();

        Assert.Equal([aObs2, aObs1], aHistory);
        Assert.Equal([bObs2, bObs1], bHistory);
        Assert.DoesNotContain(bObs1, aHistory);
        Assert.DoesNotContain(bObs2, aHistory);
        Assert.DoesNotContain(aObs1, bHistory);
        Assert.DoesNotContain(aObs2, bHistory);
    }

    [Fact]
    public void ObservationHistory_DetectsCycle_ThrowsInvalidOperationException()
    {
        var kernel = new HypergraphKernel();
        var entity = kernel.CreateVertex();

        // Hand-build a malformed 2-cycle directly via AddIncidence, bypassing RecordObservation —
        // a well-formed chain built only through RecordObservation can never form a cycle.
        var obsA = kernel.CreateVertex(VertexRoles.Edge);
        var obsB = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(obsA, entity, EntityRole, 0);
        kernel.AddIncidence(obsA, obsB, PreviousRole, 1);
        kernel.AddIncidence(obsB, entity, EntityRole, 0);
        kernel.AddIncidence(obsB, obsA, PreviousRole, 1);
        IHypergraphQuery query = kernel;

        Assert.Throws<InvalidOperationException>(() => query.ObservationHistory(obsA, PreviousRole).ToList());
    }

    [Fact]
    public void DormantObservation_StillResolvesInHistoryWalk()
    {
        // Invariant 1: dormancy tombstones, it never removes or hides a vertex from incidence-based
        // traversal. Marking an observation dormant does NOT "retire" it from history — a caller
        // wanting to stop walking should truncate ObservationHistory itself instead.
        var kernel = new HypergraphKernel();
        var entity = kernel.CreateVertex();
        var obs1 = kernel.RecordObservation(entity, EntityRole, null, PreviousRole, T0);
        var obs2 = kernel.RecordObservation(entity, EntityRole, obs1, PreviousRole, T0.AddMinutes(1));

        kernel.SetDormant(obs1);
        IHypergraphQuery query = kernel;
        var history = query.ObservationHistory(obs2, PreviousRole).ToList();

        Assert.Equal([obs2, obs1], history);
    }
}
