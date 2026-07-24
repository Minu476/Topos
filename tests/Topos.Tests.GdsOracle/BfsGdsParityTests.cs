using Neo4j.Driver;
using Topos.Hypergraph;

namespace Topos.Tests.GdsOracle;

/// <summary>
/// The first GDS-parity test (spec §5, §6 M1): runs the same reachability question through
/// Topos's own <c>IHypergraphQuery.GetBfs</c> and through Neo4j GDS's <c>gds.bfs.stream</c> over
/// the <see cref="ProjectionEngine"/>'s projection of the identical graph, and asserts the
/// domain-vertex results agree.
///
/// Skips (does not fail) if no oracle is reachable at <see cref="Neo4jTestConfig.Default"/> —
/// same graceful-skip convention RLB's own Neo4j integration tests use
/// (`RichLearning.V2.Tests/EdgeThetaPersistenceTests.cs`), so this suite doesn't break CI/dev
/// environments that haven't stood up the disposable oracle container.
/// </summary>
[Collection("GDS Oracle")]
public class BfsGdsParityTests
{
    [Fact]
    public async Task GetBfs_MatchesGdsBfsOverProjection_OnChainOfHyperedges()
    {
        var config = Neo4jTestConfig.Default;
        if (!await config.IsReachableAsync()) return; // no oracle reachable -- skip, don't fail

        // a --edge1--> b --edge2--> c  (the same chain shape as HypergraphQueryTests, mirrored
        // here so the parity check is over a known-good case, not a novel one).
        var kernel = new HypergraphKernel();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var c = kernel.CreateVertex();
        var edge1 = kernel.CreateVertex(VertexRoles.Edge);
        var edge2 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge1, a, role: 0, ordinal: 0);
        kernel.AddIncidence(edge1, b, role: 2, ordinal: 1);
        kernel.AddIncidence(edge2, b, role: 0, ordinal: 0);
        kernel.AddIncidence(edge2, c, role: 2, ordinal: 1);

        await using var driver = config.CreateDriver();
        await ProjectionEngine.ClearAsync(driver, config.Database);
        try
        {
            await ProjectionEngine.ProjectAsync(driver, config.Database, kernel);

            var topos = ((IHypergraphQuery)kernel).GetBfs(a).Select(h => h.Index).OrderBy(i => i).ToArray();
            var gds = await RunGdsBfsAsync(driver, config.Database, a.Index);

            Assert.Equal(topos, gds);
        }
        finally
        {
            await ProjectionEngine.ClearAsync(driver, config.Database);
        }
    }

    [Fact]
    public async Task GetBfs_MatchesGdsBfsOverProjection_OnRlbShapedNAryHyperedge()
    {
        var config = Neo4jTestConfig.Default;
        if (!await config.IsReachableAsync()) return;

        // RLB's actual D2 shape (spec §1.1): one Anchor, two Conditions, one Target, all
        // co-incident on a single hyperedge -- BFS from Anchor should reach every other member.
        var kernel = new HypergraphKernel();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        var anchor = kernel.CreateVertex();
        var condition1 = kernel.CreateVertex();
        var condition2 = kernel.CreateVertex();
        var target = kernel.CreateVertex();
        kernel.AddIncidence(edge, anchor, role: 0, ordinal: 0);
        kernel.AddIncidence(edge, condition1, role: 1, ordinal: 1);
        kernel.AddIncidence(edge, condition2, role: 1, ordinal: 2);
        kernel.AddIncidence(edge, target, role: 2, ordinal: 3);

        await using var driver = config.CreateDriver();
        await ProjectionEngine.ClearAsync(driver, config.Database);
        try
        {
            await ProjectionEngine.ProjectAsync(driver, config.Database, kernel);

            var topos = ((IHypergraphQuery)kernel).GetBfs(anchor).Select(h => h.Index).OrderBy(i => i).ToArray();
            var gds = await RunGdsBfsAsync(driver, config.Database, anchor.Index);

            Assert.Equal(topos, gds);
            Assert.Equal(4, topos.Length); // anchor + condition1 + condition2 + target
        }
        finally
        {
            await ProjectionEngine.ClearAsync(driver, config.Database);
        }
    }

    /// <summary>
    /// Projects the current Neo4j graph into GDS's in-memory catalog, runs <c>gds.bfs.stream</c>
    /// from the vertex with the given Topos handle index, and filters the result down to
    /// non-edge (<c>isEdge = false</c>) node handles -- the fair comparison against Topos's own
    /// <c>GetBfs</c>, per <see cref="ProjectionEngine"/>'s class doc.
    /// </summary>
    private static async Task<uint[]> RunGdsBfsAsync(IDriver driver, string database, uint startHandleIndex)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));

        const string graphName = "toposBfsParityGraph";
        await session.RunAsync("CALL gds.graph.drop($name, false)", new { name = graphName });

        // orientation: UNDIRECTED is required here, not a style choice: Topos's GetBfs treats
        // hyperedge co-membership as symmetric -- it walks Member->Source ("which edges am I on")
        // then Source->Member ("who else is on that edge"), i.e. against and then with the
        // stored INCIDENT direction. A NATURAL (directed) GDS projection only follows
        // Source->Member, so BFS from a pure member (in-degree only, no outgoing INCIDENT edges)
        // dead-ends at the start node immediately. Found by direct cypher-shell debugging when
        // the first version of this test failed with exactly that symptom.
        await session.RunAsync(
            "CALL gds.graph.project($name, 'ToposVertex', {INCIDENT: {orientation: 'UNDIRECTED'}})",
            new { name = graphName });

        try
        {
            var cursor = await session.RunAsync(
                """
                MATCH (start:ToposVertex {handle: $startHandle})
                CALL gds.bfs.stream($name, {sourceNode: start})
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
}
