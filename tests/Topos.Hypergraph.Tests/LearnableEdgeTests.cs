using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

public class LearnableEdgeTests
{
    [Fact]
    public void Uninitialized_Evaluates_ToNeutralOneHalf()
    {
        var edge = LearnableEdge.CreateUninitialized(featureCount: 3);
        Assert.Equal(0.5f, edge.Evaluate([1f, 2f, 3f]), precision: 5);
    }

    [Fact]
    public void Evaluate_KnownTheta_MatchesHandComputedSigmoid()
    {
        // theta = [bias=0, w0=1], feature=[2] -> z = 0 + 1*2 = 2 -> sigmoid(2) ~= 0.8808
        var edge = new LearnableEdge([0f, 1f]);
        float result = edge.Evaluate([2f]);
        Assert.Equal(1.0 / (1.0 + Math.Exp(-2)), result, precision: 4);
    }

    [Fact]
    public void Evaluate_FeatureLengthMismatch_Throws()
    {
        var edge = LearnableEdge.CreateUninitialized(featureCount: 2);
        Assert.Throws<ArgumentException>(() => edge.Evaluate([1f, 2f, 3f]));
    }

    [Fact]
    public void Reinforce_PositiveReward_IncreasesEvaluationOnSameFeatures()
    {
        var edge = LearnableEdge.CreateUninitialized(featureCount: 1);
        float before = edge.Evaluate([1f]);

        var reinforced = edge.Reinforce([1f], reward: 1f, learningRate: 0.5f);
        float after = reinforced.Evaluate([1f]);

        Assert.True(after > before);
    }

    [Fact]
    public void Reinforce_NegativeReward_DecreasesEvaluationOnSameFeatures()
    {
        var edge = LearnableEdge.CreateUninitialized(featureCount: 1);
        float before = edge.Evaluate([1f]);

        var reinforced = edge.Reinforce([1f], reward: -1f, learningRate: 0.5f);
        float after = reinforced.Evaluate([1f]);

        Assert.True(after < before);
    }

    [Fact]
    public void Reinforce_IsImmutable_OriginalUnaffected()
    {
        var edge = LearnableEdge.CreateUninitialized(featureCount: 1);
        var original = edge.Theta.ToArray();

        _ = edge.Reinforce([1f], reward: 1f, learningRate: 0.5f);

        Assert.Equal(original, edge.Theta);
    }

    [Fact]
    public void RoundTrips_ThroughPropertyPool_OnAnEdgeVertex()
    {
        var kernel = new HypergraphKernel();
        var learnable = kernel.ResolveProperty<LearnableEdge>("weight");
        var edge = kernel.CreateVertex(VertexRoles.Edge);

        kernel.SetProperty(learnable, edge, LearnableEdge.CreateUninitialized(2));
        Assert.True(kernel.TryGetProperty(learnable, edge, out var initial));
        Assert.Equal(0.5f, initial.Evaluate([0f, 0f]), precision: 5);

        var reinforced = initial.Reinforce([1f, 1f], reward: 1f, learningRate: 0.3f);
        kernel.SetProperty(learnable, edge, reinforced); // overwrite, same pattern as any property update

        Assert.True(kernel.TryGetProperty(learnable, edge, out var updated));
        Assert.True(updated.Evaluate([1f, 1f]) > initial.Evaluate([1f, 1f]));
    }
}
