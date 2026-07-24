namespace Topos.Hypergraph;

/// <summary>
/// Per-membership statistics carried on the edge, not the nodes — spec §1.1's RLB grounding
/// (<c>HyperEdge.TransitionCount</c>/<c>SuccessRate</c>/<c>Confidence</c>), and spec §7 pattern
/// 15's "three metadata slots," independently validated by SimpleHypergraphs.jl. Generalizes
/// RLB's fields into a standalone value usable by any consumer (M5's falsifiability gate), not
/// hard-wired to RLB's specific learning loop.
///
/// The <see cref="Observe"/> update rule (exponential moving average) is a reasonable default,
/// not a mandated one — it's a plain value type, so a consumer with a different confidence model
/// can just compute their own <see cref="EdgeStatistics"/> and <c>SetProperty</c> it instead.
/// </summary>
public readonly record struct EdgeStatistics(int TransitionCount, double SuccessRate, double Confidence)
{
    public static readonly EdgeStatistics Initial = new(TransitionCount: 0, SuccessRate: 1.0, Confidence: 0.5);

    /// <summary>One observation: increments the count and moves SuccessRate/Confidence toward the new outcome by <paramref name="smoothing"/>.</summary>
    public EdgeStatistics Observe(bool succeeded, double smoothing = 0.1)
    {
        double outcome = succeeded ? 1.0 : 0.0;
        return this with
        {
            TransitionCount = TransitionCount + 1,
            SuccessRate = SuccessRate + smoothing * (outcome - SuccessRate),
            Confidence = Math.Clamp(Confidence + (succeeded ? smoothing : -smoothing), 0.0, 1.0),
        };
    }
}
