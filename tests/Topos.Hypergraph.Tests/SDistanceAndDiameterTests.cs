using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

/// <summary>
/// T4/T3/T5 coverage for <see cref="IHypergraphQuery.GetSDistance"/>,
/// <see cref="IHypergraphQuery.GetSEccentricity"/> and
/// <see cref="IHypergraphQuery.GetSDiameter"/> (`ALGORITHM_SPEC.md` §5.3).
/// </summary>
public class SDistanceAndDiameterTests
{
    [Fact]
    public void Distance_AcrossTheWholeVertexChain()
    {
        // a -(E1)- b -(E2)- d -(E3)- e: three co-membership steps.
        var fx = SFixtures.Awkward();
        Assert.Equal(3, fx.Query.GetSDistance(fx["a"], fx["e"]));
        Assert.Equal(0, fx.Query.GetSDistance(fx["a"], fx["a"]));
        Assert.Equal(1, fx.Query.GetSDistance(fx["a"], fx["b"]));
    }

    [Fact]
    public void Distance_AcrossTheHyperedgeChain()
    {
        // E1 -(shares b,c)- E2 -(shares d)- E3.
        var fx = SFixtures.Awkward();
        Assert.Equal(2, fx.Query.GetSDistance(fx["E1"], fx["E3"], s: 1, SDomain.Hyperedges));
    }

    [Fact]
    public void Distance_IsNullWhenNoSWalkConnects()
    {
        var fx = SFixtures.Awkward();
        Assert.Null(fx.Query.GetSDistance(fx["a"], fx["f"]));                       // different components
        Assert.Null(fx.Query.GetSDistance(fx["a"], fx["e"], s: 2));                 // s=2 isolates both
        Assert.Null(fx.Query.GetSDistance(fx["a"], fx["E1"]));                      // E1 is not a vertex-domain node
    }

    [Fact]
    public void Distance_AtS2_StillFindsThePairSharingTwoEdges()
    {
        var fx = SFixtures.Awkward();
        Assert.Equal(1, fx.Query.GetSDistance(fx["b"], fx["c"], s: 2));
    }

    [Fact]
    public void Eccentricity_IsTheFarthestReachableNode()
    {
        // Connected fixture: E1={a,b,c}, E2={b,c,d}, E3={c,d,e}. c is in every edge, so it reaches
        // everything in one step; a and e sit at opposite ends and need two.
        var fx = SFixtures.Connected();
        Assert.Equal(2, fx.Query.GetSEccentricity(fx["a"]));
        Assert.Equal(2, fx.Query.GetSEccentricity(fx["b"]));
        Assert.Equal(1, fx.Query.GetSEccentricity(fx["c"]));
        Assert.Equal(2, fx.Query.GetSEccentricity(fx["d"]));
        Assert.Equal(2, fx.Query.GetSEccentricity(fx["e"]));
    }

    [Fact]
    public void Eccentricity_IsZeroForAnSIsolatedNode_AndNullForANonNode()
    {
        var fx = SFixtures.Awkward();
        Assert.Equal(0, fx.Query.GetSEccentricity(fx["f"]));            // in the domain, reaches nothing
        Assert.Null(fx.Query.GetSEccentricity(fx["E1"]));               // not a vertex-domain node at all
        Assert.Null(fx.Query.GetSEccentricity(Handle.Invalid));
    }

    [Fact]
    public void Diameter_OverBothDomains()
    {
        var fx = SFixtures.Connected();
        Assert.Equal(2, fx.Query.GetSDiameter());                        // a..e over vertices
        Assert.Equal(1, fx.Query.GetSDiameter(s: 1, SDomain.Hyperedges)); // all three edges pairwise adjacent
    }

    [Fact]
    public void Diameter_MaximizesOverConnectedPairsOnly()
    {
        // The deliberate divergence from HNX, asserted rather than assumed: HNX's diameter() raises
        // on a hypergraph that is not globally s-connected. This returns the longest s-walk that
        // does exist (a..e = 3), ignoring the unreachable f. tests/Topos.Tests.HnxOracle pins both
        // halves -- parity where HNX answers, and HNX's refusal where it does not.
        var fx = SFixtures.Awkward();
        Assert.Equal(3, fx.Query.GetSDiameter());
    }

    [Fact]
    public void Diameter_IsNullWhenNoPairIsSConnected()
    {
        var fx = SFixtures.Awkward();
        Assert.Null(fx.Query.GetSDiameter(s: 3));                        // every vertex isolates at s=3
        Assert.Null(((IHypergraphQuery)new HypergraphKernel()).GetSDiameter());
    }

    [Fact]
    public void Distance_IsSymmetricAndSatisfiesTheTriangleInequality()
    {
        // T3 metamorphic (ALGORITHM_GAP_LIST.md §4.1, properties 1-3): these hold for any correct
        // s-distance, with no reference implementation needed to state them.
        foreach (int seed in (int[])[51, 52, 53])
        {
            IHypergraphQuery kernel = SFixtures.Random(seed);
            foreach (var domain in (SDomain[])[SDomain.Vertices, SDomain.Hyperedges])
            {
                for (int s = 1; s <= 2; s++)
                {
                    var nodes = kernel.GetSLineGraph(s, domain).Keys.ToList();
                    foreach (var a in nodes)
                    {
                        Assert.Equal(0, kernel.GetSDistance(a, a, s, domain));
                        foreach (var b in nodes)
                        {
                            Assert.Equal(kernel.GetSDistance(a, b, s, domain), kernel.GetSDistance(b, a, s, domain));
                            foreach (var c in nodes)
                            {
                                int? ab = kernel.GetSDistance(a, b, s, domain);
                                int? bc = kernel.GetSDistance(b, c, s, domain);
                                int? ac = kernel.GetSDistance(a, c, s, domain);
                                if (ab is null || bc is null) continue;
                                Assert.NotNull(ac);          // a path through b exists, so a-c must be reachable
                                Assert.True(ac <= ab + bc);
                            }
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void RisingS_NeverShortensADistance()
    {
        // T3 (property 6): a stricter adjacency can only lengthen a path or break it entirely.
        foreach (int seed in (int[])[61, 62])
        {
            IHypergraphQuery kernel = SFixtures.Random(seed);
            foreach (var domain in (SDomain[])[SDomain.Vertices, SDomain.Hyperedges])
            {
                var nodes = kernel.GetSLineGraph(1, domain).Keys.ToList();
                foreach (var a in nodes)
                {
                    foreach (var b in nodes)
                    {
                        int? loose = kernel.GetSDistance(a, b, 1, domain);
                        int? strict = kernel.GetSDistance(a, b, 2, domain);
                        if (strict is null) continue;
                        Assert.NotNull(loose);
                        Assert.True(strict >= loose);
                    }
                }
            }
        }
    }

    [Fact]
    public void Diameter_IsTheLargestEccentricity()
    {
        // T3: the two are different aggregations of the same distance matrix, computed by
        // different code paths here, so they must agree.
        foreach (int seed in (int[])[71, 72])
        {
            IHypergraphQuery kernel = SFixtures.Random(seed);
            foreach (var domain in (SDomain[])[SDomain.Vertices, SDomain.Hyperedges])
            {
                for (int s = 1; s <= 3; s++)
                {
                    var nodes = kernel.GetSLineGraph(s, domain).Keys.ToList();
                    var eccentricities = nodes.Select(n => kernel.GetSEccentricity(n, s, domain)!.Value).ToList();
                    int? expected = eccentricities.Count == 0 || eccentricities.Max() == 0
                        ? null
                        : eccentricities.Max();
                    Assert.Equal(expected, kernel.GetSDiameter(s, domain));
                }
            }
        }
    }

    [Fact]
    public void Hyperedges_DistanceAgreesWithSWalk()
    {
        // T5 anchor against the pre-existing, hand-verified SWalk.Distance.
        foreach (int seed in (int[])[81, 82])
        {
            IHypergraphQuery kernel = SFixtures.Random(seed);
            for (int s = 1; s <= 3; s++)
            {
                foreach (var a in kernel.HyperedgeHandles())
                {
                    foreach (var b in kernel.HyperedgeHandles())
                    {
                        Assert.Equal(SWalk.Distance(kernel, a, b, s), kernel.GetSDistance(a, b, s, SDomain.Hyperedges));
                    }
                }
            }
        }
    }

    [Fact]
    public void Vertices_AtS1_DistanceAgreesWithTheKernelsShortestPathLength()
    {
        // T5 anchor: at s=1, vertex s-adjacency is plain co-membership, which is exactly the
        // adjacency GetShortestPathLength walks -- and that one is GDS-verified.
        foreach (int seed in (int[])[91, 92])
        {
            IHypergraphQuery kernel = SFixtures.Random(seed);
            var nodes = kernel.GetSLineGraph(1, SDomain.Vertices).Keys.ToList();
            foreach (var a in nodes)
            {
                foreach (var b in nodes)
                {
                    Assert.Equal(kernel.GetShortestPathLength(a, b), kernel.GetSDistance(a, b));
                }
            }
        }
    }

    [Fact]
    public void SBelowOne_ThrowsImmediately()
    {
        var fx = SFixtures.Awkward();
        Assert.Throws<ArgumentOutOfRangeException>(() => fx.Query.GetSDistance(fx["a"], fx["b"], s: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => fx.Query.GetSEccentricity(fx["a"], s: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => fx.Query.GetSDiameter(s: 0));
    }
}
