using Topos.Hypergraph;
using Topos.Hypergraph.Tests;

namespace Topos.Tests.HnxOracle;

/// <summary>
/// T2 parity for <see cref="IHypergraphQuery.GetSDistance"/>,
/// <see cref="IHypergraphQuery.GetSEccentricity"/> and <see cref="IHypergraphQuery.GetSDiameter"/>,
/// and — just as importantly — an assertion of the one place Topos deliberately does *not* match
/// HNX. See `IHypergraphQuery.SConnectivity.cs`: HNX refuses to compute a diameter or an
/// eccentricity unless the whole hypergraph is s-connected, while Topos maximizes over the pairs
/// that are connected, which is what spec §5.3 asks for and is defined on every input.
/// </summary>
[Collection("HnxOracle")]
public class SDistanceAndDiameterHnxParityTests
{
    [Fact]
    public async Task Distance_MatchesHnx_IncludingUnreachablePairs()
    {
        if (!await HnxOracleConfig.IsReachableAsync()) return;

        var fx = SFixtures.Awkward();
        var hypergraph = HnxExport.ToEdgeDictionary(fx.Query);

        (string From, string To, int S, SDomain Domain)[] cases =
        [
            ("a", "e", 1, SDomain.Vertices),    // right across the chain
            ("a", "f", 1, SDomain.Vertices),    // unreachable: HNX says inf, Topos says null
            ("b", "c", 2, SDomain.Vertices),    // the only s=2 vertex link
            ("E1", "E3", 1, SDomain.Hyperedges),
        ];

        foreach (var (from, to, s, domain) in cases)
        {
            var response = await HnxOracleConfig.QueryAsync(new
            {
                op = "distance",
                hypergraph,
                s,
                domain = HnxExport.Domain(domain),
                source = HnxExport.Name(fx[from]),
                target = HnxExport.Name(fx[to]),
            });

            var element = response.Ok().GetProperty("distance");
            int? expected = element.ValueKind == System.Text.Json.JsonValueKind.Null ? null : element.GetInt32();
            Assert.Equal(expected, fx.Query.GetSDistance(fx[from], fx[to], s, domain));
        }
    }

    [Fact]
    public async Task Diameter_MatchesHnx_OnAnSConnectedHypergraph()
    {
        if (!await HnxOracleConfig.IsReachableAsync()) return;

        var fx = SFixtures.Connected();
        var hypergraph = HnxExport.ToEdgeDictionary(fx.Query);

        foreach (var domain in (SDomain[])[SDomain.Vertices, SDomain.Hyperedges])
        {
            var response = await HnxOracleConfig.QueryAsync(new
            {
                op = "diameter",
                hypergraph,
                s = 1,
                domain = HnxExport.Domain(domain),
            });

            Assert.Equal(response.Ok().GetProperty("diameter").GetInt32(), fx.Query.GetSDiameter(1, domain));
        }
    }

    [Fact]
    public async Task Eccentricity_MatchesHnx_OnAnSConnectedHypergraph()
    {
        if (!await HnxOracleConfig.IsReachableAsync()) return;

        var fx = SFixtures.Connected();
        var response = await HnxOracleConfig.QueryAsync(new
        {
            op = "eccentricity",
            hypergraph = HnxExport.ToEdgeDictionary(fx.Query),
            s = 1,
            domain = "vertices",
        });

        foreach (var (name, expected) in OracleJson.Eccentricity(response.Ok()))
        {
            var handle = fx.Names.First(p => HnxExport.Name(p.Key) == name).Key;
            Assert.Equal(expected, fx.Query.GetSEccentricity(handle));
        }
    }

    [Fact]
    public async Task Diameter_DivergesFromHnx_WhenTheHypergraphIsNotSConnected()
    {
        if (!await HnxOracleConfig.IsReachableAsync()) return;

        // The divergence, pinned rather than left as prose. If a future HNX release starts
        // answering here instead of raising, this test fails and the difference gets revisited --
        // which is the point of asserting it.
        var fx = SFixtures.Awkward();
        var response = await HnxOracleConfig.QueryAsync(new
        {
            op = "diameter",
            hypergraph = HnxExport.ToEdgeDictionary(fx.Query),
            s = 1,
            domain = "vertices",
        });

        Assert.Null(response.Result);
        Assert.Contains("not s-connected", response.Error);
        Assert.Equal(3, fx.Query.GetSDiameter());
    }

    [Fact]
    public async Task OracleIsAttributable()
    {
        if (!await HnxOracleConfig.IsReachableAsync()) return;

        // A parity result means nothing without knowing which oracle produced it; the versions are
        // the ones ALGORITHM_SPEC.md §9 names.
        string description = await HnxOracleConfig.DescribeAsync();
        Assert.Contains("hypernetx=2.4.3", description);
        Assert.Contains("xgi=0.10.2", description);
    }
}
