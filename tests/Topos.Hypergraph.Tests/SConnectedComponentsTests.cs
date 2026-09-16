using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

/// <summary>
/// T4 (hand-computed golden) and T5 (reduction anchor) coverage for
/// <see cref="IHypergraphQuery.GetSConnectedComponents"/> — `ALGORITHM_GAP_LIST.md` §3's tiers.
/// The T2 oracle parity for the same fixtures lives in `tests/Topos.Tests.HnxOracle`.
/// </summary>
public class SConnectedComponentsTests
{
    [Fact]
    public void Vertices_AtS1_GroupsEveryCoMemberVertex()
    {
        // a-b-c via E1, b-c-d via E2, d-e via E3 chain into one component; f sits alone in E4.
        var fx = SFixtures.Awkward();
        Assert.Equal(
            [["a", "b", "c", "d", "e"], ["f"]],
            fx.NameComponents(fx.Query.GetSConnectedComponents(s: 1)));
    }

    [Fact]
    public void Vertices_AtS2_SplitsDownToTheOnlyPairSharingTwoEdges()
    {
        // Only b and c are co-members of two hyperedges (E1 and E2). Everything else s-isolates.
        var fx = SFixtures.Awkward();
        Assert.Equal(
            [["a"], ["b", "c"], ["d"], ["e"], ["f"]],
            fx.NameComponents(fx.Query.GetSConnectedComponents(s: 2)));
    }

    [Fact]
    public void Hyperedges_AtS1_ChainsThroughTheSingleSharedMember()
    {
        // E1-E2 share {b,c}; E2-E3 share {d}. E4 shares nothing with anything.
        var fx = SFixtures.Awkward();
        Assert.Equal(
            [["E1", "E2", "E3"], ["E4"]],
            fx.NameComponents(fx.Query.GetSConnectedComponents(s: 1, SDomain.Hyperedges)));
    }

    [Fact]
    public void Hyperedges_AtS2_DropsTheSingleSharedMemberLink()
    {
        var fx = SFixtures.Awkward();
        Assert.Equal(
            [["E1", "E2"], ["E3"], ["E4"]],
            fx.NameComponents(fx.Query.GetSConnectedComponents(s: 2, SDomain.Hyperedges)));
    }

    [Fact]
    public void Hyperedges_AtS3_LeavesEveryEdgeIsolated()
    {
        // E1 and E2 share exactly 2 members, so even they come apart at s=3.
        var fx = SFixtures.Awkward();
        Assert.Equal(
            [["E1"], ["E2"], ["E3"], ["E4"]],
            fx.NameComponents(fx.Query.GetSConnectedComponents(s: 3, SDomain.Hyperedges)));
    }

    [Fact]
    public void Vertices_ExcludeHandlesThatBelongToNoHyperedge()
    {
        // A freshly-created vertex has no s-adjacency at any s, so it is not a node of this
        // domain at all -- documented in SDomain.Vertices, and the reason Topos's node set lines
        // up with HNX's rather than carrying a singleton HNX would never report.
        var fx = SFixtures.Awkward();
        var orphan = fx.Kernel.CreateVertex();

        var components = fx.Query.GetSConnectedComponents(s: 1);
        Assert.DoesNotContain(orphan, components.SelectMany(c => c));
    }

    [Fact]
    public void NestedHyperedge_IsAVertexDomainNodeInItsOwnRight()
    {
        // Topos reifies a hyperedge as a Role:Edge vertex, so a hyperedge can itself be a member of
        // another one. SDomain.Vertices' rule -- "a node is any handle that is a member of at least
        // one hyperedge" -- covers that case without a special rule, and this pins the consequence:
        // the nested edge is a vertex-domain node co-membered with its siblings, while in the
        // hyperedge domain the two edges share no *member* and so are not s-adjacent at all.
        var kernel = new HypergraphKernel();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var c = kernel.CreateVertex();

        var inner = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(inner, a, 0, 0);
        kernel.AddIncidence(inner, b, 0, 1);

        var outer = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(outer, inner, 0, 0);
        kernel.AddIncidence(outer, c, 0, 1);

        IHypergraphQuery query = kernel;

        var vertexComponents = query.GetSConnectedComponents(s: 1);
        var withInner = vertexComponents.Single(comp => comp.Contains(inner));
        Assert.Equal([c, inner], withInner.OrderBy(h => h.Index));
        Assert.DoesNotContain(outer, vertexComponents.SelectMany(comp => comp));

        Assert.Equal(
            2,
            query.GetSConnectedComponents(s: 1, SDomain.Hyperedges).Count);
    }

    [Fact]
    public void Vertices_AtS1_ReduceToGdsVerifiedConnectedComponents()
    {
        // T5 reduction anchor (ALGORITHM_GAP_LIST.md §4.2). GetConnectedComponents is GDS-verified
        // against gds.wcc, so pinning s=1 onto it inherits that verification. It partitions the
        // *bipartite* structure, so its components also hold the hyperedge-vertices this
        // vertex-domain view has no node for -- strip those and the two must agree exactly.
        foreach (int seed in (int[])[1, 2, 3, 4, 5])
        {
            IHypergraphQuery kernel = SFixtures.Random(seed);

            // Restrict the WCC side to the vertex domain's own node set before comparing: drop the
            // hyperedge-vertices (which this domain has no node for) and the handles that belong to
            // no hyperedge (which SDomain.Vertices excludes by definition, and which WCC reports as
            // singletons). Both exclusions are stated in SDomain.Vertices' doc; neither is a fudge
            // to make a failing assertion pass -- they are what makes the two sides talk about the
            // same node set, which is the precondition for the anchor to mean anything.
            var domain = new HashSet<Handle>(
                kernel.VertexHandles().Where(h => kernel.GetVertexHyperedges(h).Count > 0));

            var wcc = kernel.GetConnectedComponents()
                .Select(c => c.Where(domain.Contains).OrderBy(h => h.Index).ToList())
                .Where(c => c.Count > 0)
                .OrderBy(c => c[0].Index)
                .ToList();

            var sComponents = kernel.GetSConnectedComponents(s: 1)
                .Select(c => c.OrderBy(h => h.Index).ToList())
                .OrderBy(c => c[0].Index)
                .ToList();

            Assert.Equal(wcc, sComponents);
        }
    }

    [Fact]
    public void Hyperedges_ReduceToSWalkReachability()
    {
        // T5 anchor on the other side: SWalk already computes edge-domain s-reachability and is
        // hand-verified by SWalkTests, so each edge's component must be exactly its SWalk-reachable
        // set. Deliberately not implemented by delegation -- two implementations agreeing is a
        // signal, one calling the other is not.
        foreach (int seed in (int[])[1, 2, 3])
        {
            IHypergraphQuery kernel = SFixtures.Random(seed);
            for (int s = 1; s <= 3; s++)
            {
                var components = kernel.GetSConnectedComponents(s, SDomain.Hyperedges);
                foreach (var edge in kernel.HyperedgeHandles())
                {
                    var component = components.Single(c => c.Contains(edge));
                    var reachable = SWalk.Reachable(kernel, edge, s).ToHashSet();
                    Assert.Equal(reachable.OrderBy(h => h.Index), component.OrderBy(h => h.Index));
                }
            }
        }
    }

    [Fact]
    public void RisingS_OnlyEverRefinesTheParitition()
    {
        // T3 metamorphic (ALGORITHM_GAP_LIST.md §4.1, property 4): a stricter adjacency can split a
        // component but can never merge two. So every component at s+1 must sit inside exactly one
        // component at s.
        foreach (int seed in (int[])[7, 8, 9])
        {
            IHypergraphQuery kernel = SFixtures.Random(seed);
            foreach (var domain in (SDomain[])[SDomain.Vertices, SDomain.Hyperedges])
            {
                for (int s = 1; s <= 3; s++)
                {
                    var coarse = kernel.GetSConnectedComponents(s, domain);
                    var finer = kernel.GetSConnectedComponents(s + 1, domain);

                    foreach (var component in finer)
                    {
                        Assert.Single(coarse, c => component.All(c.Contains));
                    }
                }
            }
        }
    }

    [Fact]
    public void ComponentsPartitionTheDomainExactly()
    {
        // T3: every node appears in exactly one component, and nothing outside the domain appears.
        foreach (int seed in (int[])[11, 12])
        {
            IHypergraphQuery kernel = SFixtures.Random(seed);
            foreach (var domain in (SDomain[])[SDomain.Vertices, SDomain.Hyperedges])
            {
                for (int s = 1; s <= 3; s++)
                {
                    var expected = kernel.GetSLineGraph(s, domain).Keys.OrderBy(h => h.Index).ToList();
                    var actual = kernel.GetSConnectedComponents(s, domain).SelectMany(c => c).ToList();

                    Assert.Equal(expected.Count, actual.Count);
                    Assert.Equal(expected, actual.OrderBy(h => h.Index));
                }
            }
        }
    }

    [Fact]
    public void SBelowOne_ThrowsImmediately()
    {
        var fx = SFixtures.Awkward();
        Assert.Throws<ArgumentOutOfRangeException>(() => fx.Query.GetSConnectedComponents(s: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => fx.Query.GetSConnectedComponents(s: -1, SDomain.Hyperedges));
    }

    [Fact]
    public void EmptyGraph_HasNoComponents()
    {
        IHypergraphQuery kernel = new HypergraphKernel();
        Assert.Empty(kernel.GetSConnectedComponents());
        Assert.Empty(kernel.GetSConnectedComponents(s: 1, SDomain.Hyperedges));
    }
}
