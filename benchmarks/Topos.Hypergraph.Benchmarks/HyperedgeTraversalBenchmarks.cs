using BenchmarkDotNet.Attributes;
using Topos.Hypergraph;

namespace Topos.Hypergraph.Benchmarks;

/// <summary>
/// M0 exit-gate benchmark, absolute gate (spec §6 M0-b): per-hop latency walking a chain of
/// RLB-shaped N-ary hyperedges (1 Anchor, 2 Conditions, 1 Target each), 5 hops per decision — the
/// figure used in the spec's own Q8 discussion.
///
/// Reports raw mean/error only — does NOT compare against a pass/fail budget, since Q8 (the
/// exact per-hop budget derived from RLB's 270Hz figure) is still open. Divide the reported mean
/// by 5 for an approximate per-hop figure once Q8 lands.
///
/// The traversal itself is hand-rolled (scan <see cref="HypergraphKernel.IncidencesOf"/> for the
/// Anchor leg, then <see cref="HypergraphKernel.IncidencesFrom"/> for the Target leg) because M1's
/// <c>IHypergraphQuery</c> doesn't exist yet — this measures the storage layer's raw traversal
/// cost, not a future query-engine's overhead on top of it.
/// </summary>
[ShortRunJob]
public class HyperedgeTraversalBenchmarks
{
    private const int Hops = 5;
    private const byte AnchorRole = 0, ConditionRole = 1, TargetRole = 2;

    private HypergraphKernel _kernel = null!;
    private Handle _start;

    [GlobalSetup]
    public void Setup()
    {
        _kernel = new HypergraphKernel();
        var anchor = _kernel.CreateVertex();
        _start = anchor;

        for (int hop = 0; hop < Hops; hop++)
        {
            var edge = _kernel.CreateVertex(VertexRoles.Edge);
            var condition1 = _kernel.CreateVertex();
            var condition2 = _kernel.CreateVertex();
            var target = _kernel.CreateVertex();

            _kernel.AddIncidence(edge, anchor, AnchorRole, ordinal: 0);
            _kernel.AddIncidence(edge, condition1, ConditionRole, ordinal: 1);
            _kernel.AddIncidence(edge, condition2, ConditionRole, ordinal: 2);
            _kernel.AddIncidence(edge, target, TargetRole, ordinal: 3);

            anchor = target; // this hop's Target is next hop's Anchor
        }
    }

    [Benchmark]
    public Handle WalkFiveHopChain()
    {
        var current = _start;
        for (int hop = 0; hop < Hops; hop++)
        {
            Incidence? anchorLeg = null;
            foreach (var m in _kernel.IncidencesOf(current))
            {
                if (m.Role == AnchorRole) { anchorLeg = m; break; }
            }
            if (anchorLeg is null) break;

            foreach (var m in _kernel.IncidencesFrom(anchorLeg.Value.Source))
            {
                if (m.Role == TargetRole) { current = m.Member; break; }
            }
        }
        return current;
    }
}
