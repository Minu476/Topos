using Topos.Hypergraph;
using Topos.Hypergraph.Tests;

namespace Topos.Tests.HnxOracle;

/// <summary>
/// T2 differential parity for <see cref="IHypergraphQuery.GetSConnectedComponents"/> against
/// HyperNetX — the oracle `ALGORITHM_SPEC.md` §3 argues for on the unusually strong ground that
/// HNX's authors are the authors of the s-walk definition itself.
///
/// Skips (passes trivially) when no oracle is reachable, per this project's established
/// oracle-test convention.
/// </summary>
[Collection("HnxOracle")]
public class SConnectedComponentsHnxParityTests
{
    [Fact]
    public async Task Awkward_MatchesHnx_AcrossSAndBothDomains()
    {
        if (!await HnxOracleConfig.IsReachableAsync()) return; // no oracle reachable -- skip, don't fail

        var fx = SFixtures.Awkward();
        var hypergraph = HnxExport.ToEdgeDictionary(fx.Query);

        foreach (var domain in (SDomain[])[SDomain.Vertices, SDomain.Hyperedges])
        {
            for (int s = 1; s <= 3; s++)
            {
                var response = await HnxOracleConfig.QueryAsync(new
                {
                    op = "s_connected_components",
                    hypergraph,
                    s,
                    domain = HnxExport.Domain(domain),
                });

                Assert.Equal(
                    OracleJson.Components(response.Ok()),
                    HnxExport.Canonical(fx.Query.GetSConnectedComponents(s, domain)));
            }
        }
    }

    [Fact]
    public async Task RandomHypergraphs_MatchHnx()
    {
        if (!await HnxOracleConfig.IsReachableAsync()) return;

        foreach (int seed in (int[])[101, 102])
        {
            IHypergraphQuery kernel = SFixtures.Random(seed);
            var hypergraph = HnxExport.ToEdgeDictionary(kernel);

            foreach (var domain in (SDomain[])[SDomain.Vertices, SDomain.Hyperedges])
            {
                for (int s = 1; s <= 2; s++)
                {
                    var response = await HnxOracleConfig.QueryAsync(new
                    {
                        op = "s_connected_components",
                        hypergraph,
                        s,
                        domain = HnxExport.Domain(domain),
                    });

                    Assert.Equal(
                        OracleJson.Components(response.Ok()),
                        HnxExport.Canonical(kernel.GetSConnectedComponents(s, domain)));
                }
            }
        }
    }
}
