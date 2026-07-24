using Neo4j.Driver;
using Topos.Hypergraph;

namespace Topos.Tests.GdsOracle;

/// <summary>M6: TriangleCount and LabelPropagation parity, over the clique-expansion projection (see <see cref="CliqueExpansionProjectionEngine"/>).</summary>
[Collection("GDS Oracle")]
public class AnalyticsGdsParityTests
{
    [Fact]
    public async Task TriangleCount_MatchesGdsTriangleCount_OnThreeMemberHyperedge()
    {
        var config = Neo4jTestConfig.Default;
        if (!await config.IsReachableAsync()) return;

        var kernel = new HypergraphKernel();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var c = kernel.CreateVertex();
        kernel.AddIncidence(edge, a, 0, 0);
        kernel.AddIncidence(edge, b, 0, 1);
        kernel.AddIncidence(edge, c, 0, 2);

        await using var driver = config.CreateDriver();
        await CliqueExpansionProjectionEngine.ClearAsync(driver, config.Database);
        try
        {
            await CliqueExpansionProjectionEngine.ProjectAsync(driver, config.Database, kernel);

            long toposCount = TriangleCount.Count(kernel);
            long gdsCount = await RunGdsTriangleCountAsync(driver, config.Database);

            Assert.Equal(toposCount, gdsCount);
            Assert.Equal(4, toposCount); // sanity: matches TriangleCountTests' hand-verified expectation
        }
        finally
        {
            await CliqueExpansionProjectionEngine.ClearAsync(driver, config.Database);
        }
    }

    [Fact]
    public async Task TriangleCount_MatchesGdsTriangleCount_OnDisjointHyperedges()
    {
        var config = Neo4jTestConfig.Default;
        if (!await config.IsReachableAsync()) return;

        var kernel = new HypergraphKernel();
        var edge1 = kernel.CreateVertex(VertexRoles.Edge);
        var a = kernel.CreateVertex(); var b = kernel.CreateVertex(); var c = kernel.CreateVertex();
        kernel.AddIncidence(edge1, a, 0, 0); kernel.AddIncidence(edge1, b, 0, 1); kernel.AddIncidence(edge1, c, 0, 2);
        var edge2 = kernel.CreateVertex(VertexRoles.Edge);
        var x = kernel.CreateVertex(); var y = kernel.CreateVertex();
        kernel.AddIncidence(edge2, x, 0, 0); kernel.AddIncidence(edge2, y, 0, 1);

        await using var driver = config.CreateDriver();
        await CliqueExpansionProjectionEngine.ClearAsync(driver, config.Database);
        try
        {
            await CliqueExpansionProjectionEngine.ProjectAsync(driver, config.Database, kernel);

            long toposCount = TriangleCount.Count(kernel);
            long gdsCount = await RunGdsTriangleCountAsync(driver, config.Database);

            Assert.Equal(toposCount, gdsCount);
            Assert.Equal(5, toposCount); // 4 from edge1's 3-member triangle + 1 from edge2's 2-member triangle
        }
        finally
        {
            await CliqueExpansionProjectionEngine.ClearAsync(driver, config.Database);
        }
    }

    [Fact]
    public async Task LabelPropagation_AgreesWithGdsOnWhichVerticesShareACommunity_ForDisconnectedClusters()
    {
        // Exact community *labels* aren't a cross-implementation invariant for label propagation
        // (multiple valid fixed points exist depending on iteration order/tie-breaking) -- but
        // for fully disconnected components, ANY correct implementation must put them in
        // different communities. That's the invariant this test checks, robust to
        // implementation differences the way BFS/WCC's exact-answer tests aren't required to be.
        var config = Neo4jTestConfig.Default;
        if (!await config.IsReachableAsync()) return;

        var kernel = new HypergraphKernel();
        var edge1 = kernel.CreateVertex(VertexRoles.Edge);
        var a = kernel.CreateVertex(); var b = kernel.CreateVertex();
        kernel.AddIncidence(edge1, a, 0, 0); kernel.AddIncidence(edge1, b, 0, 1);
        var edge2 = kernel.CreateVertex(VertexRoles.Edge);
        var x = kernel.CreateVertex(); var y = kernel.CreateVertex();
        kernel.AddIncidence(edge2, x, 0, 0); kernel.AddIncidence(edge2, y, 0, 1);

        await using var driver = config.CreateDriver();
        await CliqueExpansionProjectionEngine.ClearAsync(driver, config.Database);
        try
        {
            await CliqueExpansionProjectionEngine.ProjectAsync(driver, config.Database, kernel);

            var toposLabels = LabelPropagation.DetectCommunities(kernel);
            var gdsLabels = await RunGdsLabelPropagationAsync(driver, config.Database);

            bool ToposSame(Handle p, Handle q) => toposLabels[p] == toposLabels[q];
            bool GdsSame(Handle p, Handle q) => gdsLabels[p] == gdsLabels[q];

            Assert.True(ToposSame(a, b));
            Assert.True(GdsSame(a, b));
            Assert.True(ToposSame(x, y));
            Assert.True(GdsSame(x, y));
            Assert.False(ToposSame(a, x));
            Assert.False(GdsSame(a, x));
        }
        finally
        {
            await CliqueExpansionProjectionEngine.ClearAsync(driver, config.Database);
        }
    }

    private static async Task<long> RunGdsTriangleCountAsync(IDriver driver, string database)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        const string graphName = "toposTriangleCountParityGraph";
        await session.RunAsync("CALL gds.graph.drop($name, false)", new { name = graphName });
        await session.RunAsync(
            "CALL gds.graph.project($name, 'ToposVertex', {ADJACENT: {orientation: 'UNDIRECTED'}})",
            new { name = graphName });

        try
        {
            var cursor = await session.RunAsync(
                "CALL gds.triangleCount.stream($name) YIELD triangleCount RETURN sum(triangleCount) / 3 AS total",
                new { name = graphName });
            var records = await cursor.ToListAsync();
            return records[0]["total"].As<long>();
        }
        finally
        {
            await session.RunAsync("CALL gds.graph.drop($name, false)", new { name = graphName });
        }
    }

    private static async Task<Dictionary<Handle, long>> RunGdsLabelPropagationAsync(IDriver driver, string database)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        const string graphName = "toposLabelPropagationParityGraph";
        await session.RunAsync("CALL gds.graph.drop($name, false)", new { name = graphName });
        await session.RunAsync(
            "CALL gds.graph.project($name, 'ToposVertex', {ADJACENT: {orientation: 'UNDIRECTED'}})",
            new { name = graphName });

        try
        {
            var cursor = await session.RunAsync(
                """
                CALL gds.labelPropagation.stream($name)
                YIELD nodeId, communityId
                RETURN gds.util.asNode(nodeId).handle AS handle, communityId
                """,
                new { name = graphName });

            var records = await cursor.ToListAsync();
            return records.ToDictionary(
                r => new Handle((uint)r["handle"].As<long>()),
                r => r["communityId"].As<long>());
        }
        finally
        {
            await session.RunAsync("CALL gds.graph.drop($name, false)", new { name = graphName });
        }
    }
}
