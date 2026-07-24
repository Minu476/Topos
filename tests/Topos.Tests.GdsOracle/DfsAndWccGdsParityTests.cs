using Neo4j.Driver;
using Topos.Hypergraph;

namespace Topos.Tests.GdsOracle;

/// <summary>DFS and connected-components (WCC) parity, same conventions as <c>BfsGdsParityTests</c>.</summary>
[Collection("GDS Oracle")]
public class DfsAndWccGdsParityTests
{
    [Fact]
    public async Task GetDfs_ReachedSetMatchesGdsDfsOverProjection()
    {
        var config = Neo4jTestConfig.Default;
        if (!await config.IsReachableAsync()) return;

        var kernel = new HypergraphKernel();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var c = kernel.CreateVertex();
        var e1 = kernel.CreateVertex(VertexRoles.Edge);
        var e2 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(e1, a, 0, 0);
        kernel.AddIncidence(e1, b, 2, 1);
        kernel.AddIncidence(e2, b, 0, 0);
        kernel.AddIncidence(e2, c, 2, 1);

        await using var driver = config.CreateDriver();
        await ProjectionEngine.ClearAsync(driver, config.Database);
        try
        {
            await ProjectionEngine.ProjectAsync(driver, config.Database, kernel);

            // DFS visitation *order* isn't a cross-implementation invariant (tie-breaking
            // differs) -- only the reached set is, same reasoning as the BFS parity test.
            var topos = ((IHypergraphQuery)kernel).GetDfs(a).Select(h => h.Index).OrderBy(i => i).ToArray();
            var gds = await RunGdsDfsAsync(driver, config.Database, a.Index);

            Assert.Equal(topos, gds);
        }
        finally
        {
            await ProjectionEngine.ClearAsync(driver, config.Database);
        }
    }

    [Fact]
    public async Task GetConnectedComponents_MatchesGdsWccOverProjection()
    {
        var config = Neo4jTestConfig.Default;
        if (!await config.IsReachableAsync()) return;

        var kernel = new HypergraphKernel();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge, a, 0, 0);
        kernel.AddIncidence(edge, b, 2, 1);
        var isolated1 = kernel.CreateVertex();
        var isolated2 = kernel.CreateVertex();

        await using var driver = config.CreateDriver();
        await ProjectionEngine.ClearAsync(driver, config.Database);
        try
        {
            await ProjectionEngine.ProjectAsync(driver, config.Database, kernel);

            var topos = ((IHypergraphQuery)kernel).GetConnectedComponents()
                .Select(component => component.Select(h => h.Index).OrderBy(i => i).ToArray())
                .OrderBy(component => component[0])
                .ToArray();

            var gds = await RunGdsWccAsync(driver, config.Database);

            Assert.Equal(topos.Length, gds.Length);
            for (int i = 0; i < topos.Length; i++)
                Assert.Equal(topos[i], gds[i]);
        }
        finally
        {
            await ProjectionEngine.ClearAsync(driver, config.Database);
        }
    }

    private static async Task<uint[]> RunGdsDfsAsync(IDriver driver, string database, uint startHandleIndex)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        const string graphName = "toposDfsParityGraph";
        await session.RunAsync("CALL gds.graph.drop($name, false)", new { name = graphName });
        await session.RunAsync(
            "CALL gds.graph.project($name, 'ToposVertex', {INCIDENT: {orientation: 'UNDIRECTED'}})",
            new { name = graphName });

        try
        {
            var cursor = await session.RunAsync(
                """
                MATCH (start:ToposVertex {handle: $startHandle})
                CALL gds.dfs.stream($name, {sourceNode: start})
                YIELD path
                UNWIND nodes(path) AS n
                WITH DISTINCT n
                WHERE n.isEdge = false
                RETURN n.handle AS handle
                ORDER BY handle
                """,
                new { name = graphName, startHandle = (long)startHandleIndex });

            var records = await cursor.ToListAsync();
            return [.. records.Select(r => (uint)r["handle"].As<long>())];
        }
        finally
        {
            await session.RunAsync("CALL gds.graph.drop($name, false)", new { name = graphName });
        }
    }

    /// <summary>
    /// Runs gds.wcc.stream and returns each component as a sorted Handle-index array, the
    /// components themselves sorted by their first (smallest) member -- a canonical form so it
    /// can be compared against Topos's own partition without caring about GDS's arbitrary
    /// componentId numbering or Topos's arbitrary component-list order.
    /// </summary>
    private static async Task<uint[][]> RunGdsWccAsync(IDriver driver, string database)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        const string graphName = "toposWccParityGraph";
        await session.RunAsync("CALL gds.graph.drop($name, false)", new { name = graphName });
        await session.RunAsync(
            "CALL gds.graph.project($name, 'ToposVertex', {INCIDENT: {orientation: 'UNDIRECTED'}})",
            new { name = graphName });

        try
        {
            var cursor = await session.RunAsync(
                """
                CALL gds.wcc.stream($name)
                YIELD nodeId, componentId
                WITH componentId, gds.util.asNode(nodeId).handle AS handle
                RETURN componentId, collect(handle) AS handles
                """,
                new { name = graphName });

            var records = await cursor.ToListAsync();
            return [.. records
                .Select(r => r["handles"].As<List<object>>().Select(h => (uint)(long)h).OrderBy(i => i).ToArray())
                .OrderBy(component => component[0])];
        }
        finally
        {
            await session.RunAsync("CALL gds.graph.drop($name, false)", new { name = graphName });
        }
    }
}
