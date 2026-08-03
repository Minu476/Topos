using Neo4j.Driver;
using Topos.Hypergraph;

namespace Topos.Tests.GdsOracle;

/// <summary>
/// M11 phase 1: Degree/Closeness/Betweenness parity (spec §5.5), over the same
/// <see cref="CliqueExpansionProjectionEngine"/> projection <c>AnalyticsGdsParityTests</c> already
/// uses for Modularity/TriangleCount/LabelPropagation — <see cref="Centrality"/> shares that same
/// <see cref="BipartiteAdjacency"/>-based adjacency by design (see <see cref="Centrality"/>'s class
/// doc), so no new projection machinery is needed here.
///
/// <b>All three assert exact-value parity.</b> Degree and Betweenness are unambiguous,
/// single-definition algorithms (a distinct-neighbor count; standard unweighted Brandes) and always
/// have. Closeness's exact-value assertion was added 2026-08-03 once a live Neo4j+GDS instance
/// (`docs/GDS_ORACLE_SETUP.md`'s Docker oracle) was actually reachable and confirmed
/// <c>gds.closeness</c>'s default formula (<c>useWassermanFaustFormula = false</c>) matches
/// <see cref="Centrality.Closeness"/> to 10 decimal places on the bowtie fixture — the ranking-only
/// assertion this test previously used (kept in git history) was a documented placeholder for
/// exactly this spot-check, per `docs/DECISIONS.md`'s "M11 PHASE 1" entry.
/// </summary>
[Collection("GDS Oracle")]
public class CentralityGdsParityTests
{
    /// <summary>Two 2-member hyperedges sharing vertex B — see <c>CentralityTests.BuildBowtie</c> for the hand-derivation this shape supports.</summary>
    private static (HypergraphKernel Kernel, Handle E1, Handle A, Handle B, Handle E2, Handle C) BuildBowtie()
    {
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

        return (kernel, e1, a, b, e2, c);
    }

    [Fact]
    public async Task Degree_MatchesGdsDegree_OnBowtie()
    {
        var config = Neo4jTestConfig.Default;
        if (!await config.IsReachableAsync()) return;

        var (kernel, e1, a, b, e2, c) = BuildBowtie();

        await using var driver = config.CreateDriver();
        await CliqueExpansionProjectionEngine.ClearAsync(driver, config.Database);
        try
        {
            await CliqueExpansionProjectionEngine.ProjectAsync(driver, config.Database, kernel);

            var topos = Centrality.Degree(kernel);
            var gds = await RunGdsDegreeAsync(driver, config.Database);

            Assert.Equal(topos[e1], gds[e1]);
            Assert.Equal(topos[a], gds[a]);
            Assert.Equal(topos[b], gds[b]);
            Assert.Equal(topos[e2], gds[e2]);
            Assert.Equal(topos[c], gds[c]);
        }
        finally
        {
            await CliqueExpansionProjectionEngine.ClearAsync(driver, config.Database);
        }
    }

    [Fact]
    public async Task Betweenness_MatchesGdsBetweenness_OnBowtie()
    {
        var config = Neo4jTestConfig.Default;
        if (!await config.IsReachableAsync()) return;

        var (kernel, e1, a, b, e2, c) = BuildBowtie();

        await using var driver = config.CreateDriver();
        await CliqueExpansionProjectionEngine.ClearAsync(driver, config.Database);
        try
        {
            await CliqueExpansionProjectionEngine.ProjectAsync(driver, config.Database, kernel);

            var topos = Centrality.Betweenness(kernel);
            var gds = await RunGdsBetweennessAsync(driver, config.Database);

            Assert.Equal(topos[b], gds[b], precision: 6);
            Assert.Equal(topos[a], gds[a], precision: 6);
            Assert.Equal(topos[e1], gds[e1], precision: 6);
        }
        finally
        {
            await CliqueExpansionProjectionEngine.ClearAsync(driver, config.Database);
        }
    }

    [Fact]
    public async Task Closeness_MatchesGdsCloseness_OnBowtie()
    {
        var config = Neo4jTestConfig.Default;
        if (!await config.IsReachableAsync()) return;

        var (kernel, e1, a, b, e2, c) = BuildBowtie();

        await using var driver = config.CreateDriver();
        await CliqueExpansionProjectionEngine.ClearAsync(driver, config.Database);
        try
        {
            await CliqueExpansionProjectionEngine.ProjectAsync(driver, config.Database, kernel);

            var topos = Centrality.Closeness(kernel);
            var gds = await RunGdsClosenessAsync(driver, config.Database);

            Assert.Equal(topos[e1], gds[e1], precision: 10);
            Assert.Equal(topos[a], gds[a], precision: 10);
            Assert.Equal(topos[b], gds[b], precision: 10);
            Assert.Equal(topos[e2], gds[e2], precision: 10);
            Assert.Equal(topos[c], gds[c], precision: 10);
        }
        finally
        {
            await CliqueExpansionProjectionEngine.ClearAsync(driver, config.Database);
        }
    }

    private static async Task<Dictionary<Handle, int>> RunGdsDegreeAsync(IDriver driver, string database)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        const string graphName = "toposDegreeParityGraph";
        await session.RunAsync("CALL gds.graph.drop($name, false)", new { name = graphName });
        await session.RunAsync(
            "CALL gds.graph.project($name, 'ToposVertex', {ADJACENT: {orientation: 'UNDIRECTED'}})",
            new { name = graphName });

        try
        {
            var cursor = await session.RunAsync(
                """
                CALL gds.degree.stream($name)
                YIELD nodeId, score
                RETURN gds.util.asNode(nodeId).handle AS handle, score
                """,
                new { name = graphName });

            var records = await cursor.ToListAsync();
            return records.ToDictionary(
                r => new Handle((uint)r["handle"].As<long>()),
                r => (int)r["score"].As<double>());
        }
        finally
        {
            await session.RunAsync("CALL gds.graph.drop($name, false)", new { name = graphName });
        }
    }

    private static async Task<Dictionary<Handle, double>> RunGdsBetweennessAsync(IDriver driver, string database)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        const string graphName = "toposBetweennessParityGraph";
        await session.RunAsync("CALL gds.graph.drop($name, false)", new { name = graphName });
        await session.RunAsync(
            "CALL gds.graph.project($name, 'ToposVertex', {ADJACENT: {orientation: 'UNDIRECTED'}})",
            new { name = graphName });

        try
        {
            var cursor = await session.RunAsync(
                """
                CALL gds.betweenness.stream($name)
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

    private static async Task<Dictionary<Handle, double>> RunGdsClosenessAsync(IDriver driver, string database)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        const string graphName = "toposClosenessParityGraph";
        await session.RunAsync("CALL gds.graph.drop($name, false)", new { name = graphName });
        await session.RunAsync(
            "CALL gds.graph.project($name, 'ToposVertex', {ADJACENT: {orientation: 'UNDIRECTED'}})",
            new { name = graphName });

        try
        {
            var cursor = await session.RunAsync(
                """
                CALL gds.closeness.stream($name)
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
