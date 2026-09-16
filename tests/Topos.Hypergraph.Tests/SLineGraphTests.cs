using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

/// <summary>
/// T4 (hand-computed golden) and T3 (metamorphic) coverage for
/// <see cref="IHypergraphQuery.GetSLineGraph"/>. This is the item `ALGORITHM_GAP_LIST.md` §4.5
/// rates "high confidence, 2 independent impls" — the two implementations being HyperNetX and xgi,
/// which `tests/Topos.Tests.HnxOracle` checks against these same fixtures.
/// </summary>
public class SLineGraphTests
{
    [Fact]
    public void Hyperedges_AtS1_LinksEdgesSharingAnyMember()
    {
        var fx = SFixtures.Awkward();
        Assert.Equal(
            new Dictionary<string, List<string>>
            {
                ["E1"] = ["E2"],          // shares {b,c}
                ["E2"] = ["E1", "E3"],    // shares {b,c} with E1, {d} with E3
                ["E3"] = ["E2"],
                ["E4"] = [],              // {f} is in nothing else
            },
            fx.NameAdjacency(fx.Query.GetSLineGraph(s: 1)));
    }

    [Fact]
    public void Hyperedges_AtS2_KeepsOnlyTheTwoMemberOverlap()
    {
        var fx = SFixtures.Awkward();
        Assert.Equal(
            new Dictionary<string, List<string>>
            {
                ["E1"] = ["E2"],
                ["E2"] = ["E1"],
                ["E3"] = [],
                ["E4"] = [],
            },
            fx.NameAdjacency(fx.Query.GetSLineGraph(s: 2)));
    }

    [Fact]
    public void Vertices_AtS1_IsTheCoMembershipGraph()
    {
        // The dual reading: nodes are vertices, linked when they co-occur in a hyperedge. This is
        // HNX's get_linegraph(edges=False), and it is the adjacency GetSConnectedComponents and
        // GetSDiameter run over in their default domain -- exposing it is what makes that
        // derivation directly oracle-checkable rather than only checkable through its results.
        var fx = SFixtures.Awkward();
        Assert.Equal(
            new Dictionary<string, List<string>>
            {
                ["a"] = ["b", "c"],
                ["b"] = ["a", "c", "d"],
                ["c"] = ["a", "b", "d"],
                ["d"] = ["b", "c", "e"],
                ["e"] = ["d"],
                ["f"] = [],
            },
            fx.NameAdjacency(fx.Query.GetSLineGraph(s: 1, SDomain.Vertices)));
    }

    [Fact]
    public void Vertices_AtS2_KeepsOnlyThePairSharingTwoEdges()
    {
        var fx = SFixtures.Awkward();
        Assert.Equal(
            new Dictionary<string, List<string>>
            {
                ["a"] = [], ["b"] = ["c"], ["c"] = ["b"], ["d"] = [], ["e"] = [], ["f"] = [],
            },
            fx.NameAdjacency(fx.Query.GetSLineGraph(s: 2, SDomain.Vertices)));
    }

    [Fact]
    public void IsolatedNodesKeepTheirKey()
    {
        // E4 has one member and can never be s-adjacent to anything, but it is still a node of the
        // line graph. HNX and xgi both keep it; dropping it would silently disagree with both
        // about the node set while still "passing" any edge-by-edge comparison.
        var fx = SFixtures.Awkward();
        for (int s = 1; s <= 3; s++)
        {
            Assert.Contains(fx["E4"], fx.Query.GetSLineGraph(s).Keys);
        }
    }

    [Fact]
    public void AdjacencyIsSymmetric()
    {
        // T3: s-adjacency is defined by set overlap, which is symmetric -- so the line graph must
        // be undirected in fact, not just by intent.
        foreach (int seed in (int[])[21, 22, 23])
        {
            IHypergraphQuery kernel = SFixtures.Random(seed);
            foreach (var domain in (SDomain[])[SDomain.Vertices, SDomain.Hyperedges])
            {
                for (int s = 1; s <= 3; s++)
                {
                    var graph = kernel.GetSLineGraph(s, domain);
                    foreach (var (node, neighbors) in graph)
                    {
                        Assert.DoesNotContain(node, neighbors);                       // no self-loops
                        Assert.Equal(neighbors.Distinct().Count(), neighbors.Count);   // no duplicate links
                        foreach (var neighbor in neighbors) Assert.Contains(node, graph[neighbor]);
                    }
                }
            }
        }
    }

    [Fact]
    public void RisingS_OnlyEverRemovesLinks()
    {
        // T3: a higher s is a strictly stricter threshold on the same share-counts, so the s+1
        // line graph must be a subgraph of the s one.
        foreach (int seed in (int[])[31, 32])
        {
            IHypergraphQuery kernel = SFixtures.Random(seed);
            foreach (var domain in (SDomain[])[SDomain.Vertices, SDomain.Hyperedges])
            {
                for (int s = 1; s <= 3; s++)
                {
                    var coarse = kernel.GetSLineGraph(s, domain);
                    var finer = kernel.GetSLineGraph(s + 1, domain);

                    Assert.Equal(coarse.Keys.OrderBy(h => h.Index), finer.Keys.OrderBy(h => h.Index));
                    foreach (var (node, neighbors) in finer)
                    {
                        foreach (var neighbor in neighbors) Assert.Contains(neighbor, coarse[node]);
                    }
                }
            }
        }
    }

    [Fact]
    public void Hyperedges_AgreeWithSWalkAdjacency()
    {
        // T5 anchor: one s-adjacency step is exactly SWalk.Distance == 1, and SWalk is the
        // hand-verified implementation that predates this one.
        foreach (int seed in (int[])[41, 42])
        {
            IHypergraphQuery kernel = SFixtures.Random(seed);
            for (int s = 1; s <= 3; s++)
            {
                var graph = kernel.GetSLineGraph(s, SDomain.Hyperedges);
                foreach (var edge in kernel.HyperedgeHandles())
                {
                    foreach (var other in kernel.HyperedgeHandles())
                    {
                        if (edge == other) continue;
                        bool linked = graph[edge].Contains(other);
                        Assert.Equal(SWalk.Distance(kernel, edge, other, s) == 1, linked);
                    }
                }
            }
        }
    }

    [Fact]
    public void SBelowOne_ThrowsImmediately()
    {
        var fx = SFixtures.Awkward();
        Assert.Throws<ArgumentOutOfRangeException>(() => fx.Query.GetSLineGraph(s: 0));
    }
}
