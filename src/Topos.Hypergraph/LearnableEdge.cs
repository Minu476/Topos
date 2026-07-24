namespace Topos.Hypergraph;

/// <summary>
/// A learnable edge weight: sigmoid activation over a linear combination of features, updated by
/// gradient ascent on reward (spec §6 M5: "learnable edges... DHG's injection point"). Generalizes
/// RLB's <c>HyperEdge.ThetaParameters</c>/<c>ReinforceTheta</c> (spec §1.1) — same math, but not
/// tied to RLB's fixed 7-slot <c>EdgeFeatureLayout</c>: the feature vector length here is whatever
/// the caller supplies, so this is usable by a consumer that isn't RLB (the whole point of M5's
/// falsifiability gate).
///
/// Immutable value type, like every other typed attribute in Topos. There's no separate mutation
/// API — <see cref="Reinforce"/> returns a new <see cref="LearnableEdge"/>, and the caller
/// <c>SetProperty</c>s it back over the old one, exactly like updating any other property.
/// </summary>
public readonly record struct LearnableEdge(float[] Theta)
{
    /// <summary>v = sigmoid(theta · [1, features...]) — theta[0] is the bias term.</summary>
    public float Evaluate(ReadOnlySpan<float> features)
    {
        if (features.Length != Theta.Length - 1)
            throw new ArgumentException(
                $"Feature vector length {features.Length} doesn't match this edge's Theta length " +
                $"{Theta.Length} (expects {Theta.Length - 1} features + 1 bias).");

        float z = Theta[0];
        for (int i = 0; i < features.Length; i++) z += Theta[i + 1] * features[i];
        return 1f / (1f + MathF.Exp(-z));
    }

    /// <summary>
    /// One gradient-ascent step toward <paramref name="reward"/> — same math as RLB's
    /// <c>ReinforceTheta</c> (spec §1.1): <c>grad = v(1-v)</c>, <c>step = learningRate * reward * grad</c>.
    /// </summary>
    public LearnableEdge Reinforce(ReadOnlySpan<float> features, float reward, float learningRate)
    {
        float v = Evaluate(features);
        float grad = v * (1f - v);
        float step = learningRate * reward * grad;

        var next = new float[Theta.Length];
        next[0] = Theta[0] + step;
        for (int i = 0; i < features.Length; i++)
            next[i + 1] = Theta[i + 1] + step * features[i];

        return new LearnableEdge(next);
    }

    /// <summary>An all-zero edge — <see cref="Evaluate"/> returns 0.5 (neutral) until reinforced, matching RLB's uninitialized-theta convention.</summary>
    public static LearnableEdge CreateUninitialized(int featureCount) => new(new float[featureCount + 1]);
}
