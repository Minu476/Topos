using Neo4j.Driver;
using Topos.Hypergraph;

namespace Topos.Tests.GdsOracle;

/// <summary>
/// M11 phase 1: PageRank parity (spec §5.6), over the same <see cref="CliqueExpansionProjectionEngine"/>
/// projection <see cref="CentralityGdsParityTests"/> uses — <see cref="PageRank"/> shares
/// <see cref="Centrality"/>'s <see cref="BipartiteAdjacency"/>-based adjacency by design. Default
/// damping (0.85) matches <c>gds.pageRank</c>'s own documented default.
///
/// <b>L1-normalized before comparison — a real finding from the first live-oracle run</b>
/// (2026-08-03, `docs/GDS_ORACLE_SETUP.md`'s Docker oracle): <c>gds.pageRank</c>'s default output is
/// <b>not</b> a probability distribution — its base term is the classic <c>(1-d)</c> per node, not
/// <c>(1-d)/N</c>, so its raw scores sum to ~N rather than 1 (confirmed empirically: on this file's
/// 5-vertex bowtie, every GDS raw score was ~5× the matching <see cref="PageRank.Compute"/> value,
/// N=5). <see cref="PageRank.Compute"/>'s own contract (sums to 1.0 — see its XML doc) is the
/// intentional, documented choice; GDS's raw scores are rescaled here by dividing by their own sum
/// so the comparison is apples-to-apples on the *relative* distribution, which is what both
/// implementations actually agree on. (GDS 2.x+ also exposes a <c>scaler: 'L1Norm'</c> config
/// option that does this server-side; normalizing client-side here avoids a version dependency.)
/// </summary>
[Collection("GDS Oracle")]
public class PageRankGdsParityTests
{
    [Fact]
    public async Task Compute_MatchesGdsPageRank_OnBowtie()
    {
        var config = Neo4jTestConfig.Default;
        if (!await config.IsReachableAsync()) return;

        var kernel = new HypergraphKernel();
        var e1 = kernel.CreateVertex(VertexRoles.Edge);
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        kernel.AddIncidence(e1, a, 0, 0);
        kernel.AddIncidence(e1, b, 0, 1);

        var e2 = kernel.CreateVertex(VertexRoles.Edge);
        var c = kernel.CreateVertex();
        kernel.AddIncidence(e2, b, 0, 0);
        kernel.AddIncidence(e2, c, 0, 1);

        await using var driver = config.CreateDriver();
        await CliqueExpansionProjectionEngine.ClearAsync(driver, config.Database);
        try
        {
            await CliqueExpansionProjectionEngine.ProjectAsync(driver, config.Database, kernel);

            var topos = PageRank.Compute(kernel);
            var gdsRaw = await RunGdsPageRankAsync(driver, config.Database);
            double gdsSum = gdsRaw.Values.Sum();
            var gds = gdsRaw.ToDictionary(kv => kv.Key, kv => kv.Value / gdsSum);

            Assert.Equal(topos[e1], gds[e1], precision: 4);
            Assert.Equal(topos[a], gds[a], precision: 4);
            Assert.Equal(topos[b], gds[b], precision: 4);
            Assert.Equal(topos[e2], gds[e2], precision: 4);
            Assert.Equal(topos[c], gds[c], precision: 4);
        }
        finally
        {
            await CliqueExpansionProjectionEngine.ClearAsync(driver, config.Database);
        }
    }

    private static async Task<Dictionary<Handle, double>> RunGdsPageRankAsync(IDriver driver, string database)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        const string graphName = "toposPageRankParityGraph";
        await session.RunAsync("CALL gds.graph.drop($name, false)", new { name = graphName });
        await session.RunAsync(
            "CALL gds.graph.project($name, 'ToposVertex', {ADJACENT: {orientation: 'UNDIRECTED'}})",
            new { name = graphName });

        try
        {
            var cursor = await session.RunAsync(
                """
                CALL gds.pageRank.stream($name, {dampingFactor: 0.85, maxIterations: 100, tolerance: 0.000001})
                YIELD nodeId, score
                RETURN gds.util.asNode(nodeId).handle AS handle, score
                """,
                new { name = graphName });

            var records = await cursor.ToListAsync();
            return records.ToDictionary(
                r => new Handle((uint)r["handle"].As<long>()),
                r => r["score"].As<double>());
        }
        finally
        {
            await session.RunAsync("CALL gds.graph.drop($name, false)", new { name = graphName });
        }
    }
}
