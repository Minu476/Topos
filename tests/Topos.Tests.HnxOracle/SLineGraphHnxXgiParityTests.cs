using Topos.Hypergraph;
using Topos.Hypergraph.Tests;

namespace Topos.Tests.HnxOracle;

/// <summary>
/// T2 differential parity for <see cref="IHypergraphQuery.GetSLineGraph"/> against <b>two</b>
/// independent implementations — HyperNetX (PNNL) and xgi (Landry/Young et al.), which cite the
/// same Aksoy 2020 definition but share no code. `ALGORITHM_GAP_LIST.md` §4.3 is explicit about
/// why that pairing is worth more than either alone: agreement across separate lineages is
/// corroboration, where agreement with a single oracle could just be a shared reading of one paper.
/// </summary>
[Collection("HnxOracle")]
public class SLineGraphHnxXgiParityTests
{
    [Fact]
    public async Task Awkward_MatchesHnx_AcrossSAndBothDomains()
    {
        if (!await HnxOracleConfig.IsReachableAsync()) return;

        var fx = SFixtures.Awkward();
        var hypergraph = HnxExport.ToEdgeDictionary(fx.Query);

        foreach (var domain in (SDomain[])[SDomain.Vertices, SDomain.Hyperedges])
        {
            for (int s = 1; s <= 2; s++)
            {
                var response = await HnxOracleConfig.QueryAsync(new
                {
                    op = "linegraph",
                    hypergraph,
                    s,
                    domain = HnxExport.Domain(domain),
                });

                Assert.Equal(
                    OracleJson.Adjacency(response.Ok()),
                    HnxExport.Canonical(fx.Query.GetSLineGraph(s, domain)));
            }
        }
    }

    [Fact]
    public async Task Awkward_MatchesXgi()
    {
        if (!await HnxOracleConfig.IsReachableAsync()) return;

        // xgi's line graph is hyperedge-noded only, so this covers the default domain.
        var fx = SFixtures.Awkward();
        var hypergraph = HnxExport.ToEdgeDictionary(fx.Query);

        for (int s = 1; s <= 2; s++)
        {
            var response = await HnxOracleConfig.QueryAsync(new { op = "xgi_linegraph", hypergraph, s });
            Assert.Equal(
                OracleJson.Adjacency(response.Ok()),
                HnxExport.Canonical(fx.Query.GetSLineGraph(s, SDomain.Hyperedges)));
        }
    }

    [Fact]
    public async Task RandomHypergraphs_MatchBothLineages()
    {
        if (!await HnxOracleConfig.IsReachableAsync()) return;

        foreach (int seed in (int[])[111, 112])
        {
            IHypergraphQuery kernel = SFixtures.Random(seed);
            var hypergraph = HnxExport.ToEdgeDictionary(kernel);

            for (int s = 1; s <= 2; s++)
            {
                var topos = HnxExport.Canonical(kernel.GetSLineGraph(s, SDomain.Hyperedges));

                var hnx = OracleJson.Adjacency(
                    (await HnxOracleConfig.QueryAsync(new { op = "linegraph", hypergraph, s, domain = "hyperedges" })).Ok());
                var xgi = OracleJson.Adjacency(
                    (await HnxOracleConfig.QueryAsync(new { op = "xgi_linegraph", hypergraph, s })).Ok());

                Assert.Equal(hnx, topos);
                Assert.Equal(xgi, topos);
                // Stated separately on purpose: if the two lineages ever disagree with each other,
                // that is a finding about the definition, and it should not be reported as a Topos
                // failure.
                Assert.Equal(hnx, xgi);
            }
        }
    }
}
