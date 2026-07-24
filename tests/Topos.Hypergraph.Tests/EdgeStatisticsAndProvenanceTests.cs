using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

public class EdgeStatisticsAndProvenanceTests
{
    [Fact]
    public void Initial_HasNeutralDefaults()
    {
        Assert.Equal(0, EdgeStatistics.Initial.TransitionCount);
        Assert.Equal(1.0, EdgeStatistics.Initial.SuccessRate);
        Assert.Equal(0.5, EdgeStatistics.Initial.Confidence);
    }

    [Fact]
    public void Observe_Success_IncrementsCountAndRaisesConfidence()
    {
        var stats = EdgeStatistics.Initial.Observe(succeeded: true);

        Assert.Equal(1, stats.TransitionCount);
        Assert.True(stats.Confidence > EdgeStatistics.Initial.Confidence);
    }

    [Fact]
    public void Observe_Failure_IncrementsCountAndLowersConfidenceAndSuccessRate()
    {
        var stats = EdgeStatistics.Initial.Observe(succeeded: false);

        Assert.Equal(1, stats.TransitionCount);
        Assert.True(stats.Confidence < EdgeStatistics.Initial.Confidence);
        Assert.True(stats.SuccessRate < EdgeStatistics.Initial.SuccessRate);
    }

    [Fact]
    public void Observe_Repeatedly_ConfidenceStaysClampedToUnitRange()
    {
        var stats = EdgeStatistics.Initial;
        for (int i = 0; i < 100; i++) stats = stats.Observe(succeeded: true);

        Assert.Equal(100, stats.TransitionCount);
        Assert.InRange(stats.Confidence, 0.0, 1.0);
    }

    [Fact]
    public void EdgeStatistics_RoundTripsThroughPropertyPool()
    {
        var kernel = new HypergraphKernel();
        var stats = kernel.ResolveProperty<EdgeStatistics>("stats");
        var edge = kernel.CreateVertex(VertexRoles.Edge);

        kernel.SetProperty(stats, edge, EdgeStatistics.Initial.Observe(true).Observe(true).Observe(false));

        Assert.True(kernel.TryGetProperty(stats, edge, out var recorded));
        Assert.Equal(3, recorded.TransitionCount);
    }

    [Fact]
    public void Provenance_LeafCase_RoundTripsAsAnOrdinaryProperty()
    {
        var kernel = new HypergraphKernel();
        var provenance = kernel.ResolveProperty<Provenance>("provenance");
        var fact = kernel.CreateVertex(VertexRoles.Edge);

        var recordedAt = new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
        kernel.SetProperty(provenance, fact, new Provenance("user-message-42", recordedAt));

        Assert.True(kernel.TryGetProperty(provenance, fact, out var p));
        Assert.Equal("user-message-42", p.Source);
        Assert.Equal(recordedAt, p.RecordedAt);
    }

    [Fact]
    public void Provenance_StructuralCase_ExpressedViaNestedReification()
    {
        // A fact derived FROM other facts (not an external leaf source) uses the M2 mechanism:
        // the derived edge nests its sources as members, exactly like ReificationTests' chain.
        var kernel = new HypergraphKernel();
        var sourceFactA = kernel.CreateVertex(VertexRoles.Edge);
        var sourceFactB = kernel.CreateVertex(VertexRoles.Edge);

        var derivedFact = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(derivedFact, sourceFactA, role: 1 /* derived-from */, ordinal: 0);
        kernel.AddIncidence(derivedFact, sourceFactB, role: 1, ordinal: 1);

        var derivationSources = kernel.IncidencesFrom(derivedFact);
        Assert.Equal(2, derivationSources.Length);
        Assert.Contains(derivationSources, i => i.Member == sourceFactA);
        Assert.Contains(derivationSources, i => i.Member == sourceFactB);
    }
}
