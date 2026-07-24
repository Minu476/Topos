using Neo4j.Driver;
using Topos.Hypergraph;

namespace Topos.Tests.GdsOracle;

/// <summary>
/// Shortest-path-length parity via <c>gds.shortestPath.dijkstra.stream</c>. Dijkstra defaults to
/// unit edge weight when no <c>relationshipWeightProperty</c> is given — exactly "unweighted
/// shortest path," which this graph needs since it has no weight property at all.
///
/// <b>Not <c>gds.bfs.stream</c>, found by debugging a real failure:</b> the first version of this
/// test used BFS with a <c>targetNodes</c> filter and got length 3 for what direct
/// <c>cypher-shell</c> verification (both plain Cypher <c>shortestPath()</c> and GDS Dijkstra)
/// confirmed was a 2-hop path. Root cause: <c>gds.bfs.stream</c> without per-node distance
/// tracking returns a single traversal-order node sequence over everything reached, not a
/// shortest-path tree — a node's position in that sequence isn't its BFS distance. Fine for the
/// "what did BFS/DFS reach" comparisons in <c>BfsGdsParityTests</c>/<c>DfsAndWccGdsParityTests</c>
/// (order-independent set comparison), wrong for path *length*. Dijkstra is the correct oracle.
///
/// <b>The bipartite-hop-count halving, explained.</b> GDS walks the projected graph directly,
/// where a single Topos-logical hop (vertex → shared hyperedge → other vertex) is *two* bipartite
/// edges. A GDS path of length <c>k</c> (k+1 nodes, alternating vertex/edge/vertex/edge/...) is
/// exactly <c>k/2</c> logical hops — <c>k</c> is always even for a path between two domain
/// vertices, since the projection strictly alternates node "color" by construction (an INCIDENT
/// edge only ever connects an edge-node to a member-node, never same-to-same).
/// </summary>
[Collection("GDS Oracle")]
public class ShortestPathGdsParityTests
{
    [Fact]
    public async Task GetShortestPathLength_MatchesHalvedGdsBfsPathLength()
    {
        var config = Neo4jTestConfig.Default;
        if (!await config.IsReachableAsync()) return;

        // a --e1--> b --e2--> c --e3--> d : Topos length a->d should be 3.
        var kernel = new HypergraphKernel();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var c = kernel.CreateVertex();
        var d = kernel.CreateVertex();
        var e1 = kernel.CreateVertex(VertexRoles.Edge);
        var e2 = kernel.CreateVertex(VertexRoles.Edge);
        var e3 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(e1, a, 0, 0); kernel.AddIncidence(e1, b, 2, 1);
        kernel.AddIncidence(e2, b, 0, 0); kernel.AddIncidence(e2, c, 2, 1);
        kernel.AddIncidence(e3, c, 0, 0); kernel.AddIncidence(e3, d, 2, 1);

        await using var driver = config.CreateDriver();
        await ProjectionEngine.ClearAsync(driver, config.Database);
        try
        {
            await ProjectionEngine.ProjectAsync(driver, config.Database, kernel);

            var toposLength = ((IHypergraphQuery)kernel).GetShortestPathLength(a, d);
            var gdsBipartiteLength = await RunGdsShortestPathAsync(driver, config.Database, a.Index, d.Index);

            Assert.NotNull(toposLength);
            Assert.NotNull(gdsBipartiteLength);
            Assert.Equal(0, gdsBipartiteLength % 2); // always even -- see class doc
            Assert.Equal(toposLength, gdsBipartiteLength / 2);
        }
        finally
        {
            await ProjectionEngine.ClearAsync(driver, config.Database);
        }
    }

    [Fact]
    public async Task GetShortestPathLength_TakesShorterRoute_MatchesGds()
    {
        var config = Neo4jTestConfig.Default;
        if (!await config.IsReachableAsync()) return;

        // a direct to c (1 hop) vs. a-b-c (2 hops) -- both Topos and GDS must pick the shorter.
        var kernel = new HypergraphKernel();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var c = kernel.CreateVertex();
        var shortEdge = kernel.CreateVertex(VertexRoles.Edge);
        var e1 = kernel.CreateVertex(VertexRoles.Edge);
        var e2 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(shortEdge, a, 0, 0); kernel.AddIncidence(shortEdge, c, 2, 1);
        kernel.AddIncidence(e1, a, 0, 0); kernel.AddIncidence(e1, b, 2, 1);
        kernel.AddIncidence(e2, b, 0, 0); kernel.AddIncidence(e2, c, 2, 1);

        await using var driver = config.CreateDriver();
        await ProjectionEngine.ClearAsync(driver, config.Database);
        try
        {
            await ProjectionEngine.ProjectAsync(driver, config.Database, kernel);

            var toposLength = ((IHypergraphQuery)kernel).GetShortestPathLength(a, c);
            var gdsBipartiteLength = await RunGdsShortestPathAsync(driver, config.Database, a.Index, c.Index);

            Assert.Equal(1, toposLength);
            Assert.Equal(2, gdsBipartiteLength); // 1 logical hop = 2 bipartite hops
        }
        finally
        {
            await ProjectionEngine.ClearAsync(driver, config.Database);
        }
    }

    private static async Task<int?> RunGdsShortestPathAsync(
        IDriver driver, string database, uint fromHandleIndex, uint toHandleIndex)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        const string graphName = "toposShortestPathParityGraph";
        await session.RunAsync("CALL gds.graph.drop($name, false)", new { name = graphName });
        await session.RunAsync(
            "CALL gds.graph.project($name, 'ToposVertex', {INCIDENT: {orientation: 'UNDIRECTED'}})",
            new { name = graphName });

        try
        {
            // NOT gds.bfs.stream: with a source and no/multiple targets it returns a single
            // traversal-order sequence over *all* reached nodes, not a shortest-path tree -- a
            // node's position in that sequence is not its BFS distance (found by direct
            // cypher-shell debugging: it returned length 3 for a 2-hop path here). Dijkstra
            // defaults to unit edge weight when no relationshipWeightProperty is given, which is
            // exactly "unweighted shortest path" and does return the correct tree.
            var cursor = await session.RunAsync(
                """
                MATCH (start:ToposVertex {handle: $from}), (end:ToposVertex {handle: $to})
                CALL gds.shortestPath.dijkstra.stream($name, {sourceNode: start, targetNode: end})
                YIELD path
                RETURN length(path) AS bipartiteLength
                """,
                new { name = graphName, from = (long)fromHandleIndex, to = (long)toHandleIndex });

            var records = await cursor.ToListAsync();
            return records.Count == 0 ? null : (int)records[0]["bipartiteLength"].As<long>();
        }
        finally
        {
            await session.RunAsync("CALL gds.graph.drop($name, false)", new { name = graphName });
        }
    }
}
