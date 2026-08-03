using Neo4j.Driver;
using Topos.Hypergraph;
using Topos.Hypergraph.Knowledge;

namespace Topos.Tests.GdsOracle;

/// <summary>
/// M11 phase 1: <see cref="DirectedTraversal.DirectedScc"/> parity (spec §5.4) — projects only the
/// role-qualifying <c>fromRole</c>→<c>toRole</c> legs as direct <c>:ROLE_EDGE</c> relationships
/// (skipping the hyperedge/Condition scaffolding entirely, unlike <see cref="ProjectionEngine"/>'s
/// bipartite <c>:INCIDENT</c> projection), then runs <c>gds.scc</c> directly — the clean binary
/// projection spec §5.4 describes ("project Anchor→Target legs as directed binary edges"), built
/// fresh here rather than reusing <see cref="ProjectionEngine"/> or
/// <see cref="CliqueExpansionProjectionEngine"/>, neither of which is directed in the right shape.
///
/// Compares which vertices share a component, not raw <c>componentId</c> values (arbitrary labels)
/// — the same convention <c>AnalyticsGdsParityTests</c>'s LabelPropagation test and
/// <c>DirectedSccTests</c>' own unit tests use.
/// </summary>
[Collection("GDS Oracle")]
public class DirectedSccGdsParityTests
{
    private const byte AnchorRole = 0, TargetRole = 2;

    [Fact]
    public async Task DirectedScc_AgreesWithGdsScc_OnThreeCycleOfBinaryLegs()
    {
        var config = Neo4jTestConfig.Default;
        if (!await config.IsReachableAsync()) return;

        var kernel = new HypergraphKernel();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var c = kernel.CreateVertex();
        var edge1 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge1, a, AnchorRole, 0);
        kernel.AddIncidence(edge1, b, TargetRole, 1);
        var edge2 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge2, b, AnchorRole, 0);
        kernel.AddIncidence(edge2, c, TargetRole, 1);
        var edge3 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge3, c, AnchorRole, 0);
        kernel.AddIncidence(edge3, a, TargetRole, 1);

        await using var driver = config.CreateDriver();
        await ClearAsync(driver, config.Database);
        try
        {
            await ProjectRoleGraphAsync(driver, config.Database, kernel, AnchorRole, TargetRole);

            IHypergraphQuery query = kernel;
            var topos = query.DirectedScc(AnchorRole, TargetRole);
            var gds = await RunGdsSccAsync(driver, config.Database);

            bool ToposSame(Handle p, Handle q) => topos.Any(comp => comp.Contains(p) && comp.Contains(q));
            bool GdsSame(Handle p, Handle q) => gds[p] == gds[q];

            Assert.True(ToposSame(a, b));
            Assert.True(GdsSame(a, b));
            Assert.True(ToposSame(b, c));
            Assert.True(GdsSame(b, c));
            Assert.False(ToposSame(a, edge1));
            Assert.False(GdsSame(a, edge1));
        }
        finally
        {
            await ClearAsync(driver, config.Database);
        }
    }

    [Fact]
    public async Task DirectedScc_AgreesWithGdsScc_OnAcyclicChain()
    {
        var config = Neo4jTestConfig.Default;
        if (!await config.IsReachableAsync()) return;

        var kernel = new HypergraphKernel();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var c = kernel.CreateVertex();
        var edge1 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge1, a, AnchorRole, 0);
        kernel.AddIncidence(edge1, b, TargetRole, 1);
        var edge2 = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge2, b, AnchorRole, 0);
        kernel.AddIncidence(edge2, c, TargetRole, 1);

        await using var driver = config.CreateDriver();
        await ClearAsync(driver, config.Database);
        try
        {
            await ProjectRoleGraphAsync(driver, config.Database, kernel, AnchorRole, TargetRole);

            IHypergraphQuery query = kernel;
            var topos = query.DirectedScc(AnchorRole, TargetRole);
            var gds = await RunGdsSccAsync(driver, config.Database);

            bool ToposSame(Handle p, Handle q) => topos.Any(comp => comp.Contains(p) && comp.Contains(q));
            bool GdsSame(Handle p, Handle q) => gds[p] == gds[q];

            Assert.False(ToposSame(a, b));
            Assert.False(GdsSame(a, b));
            Assert.False(ToposSame(b, c));
            Assert.False(GdsSame(b, c));
        }
        finally
        {
            await ClearAsync(driver, config.Database);
        }
    }

    private static async Task ProjectRoleGraphAsync(
        IDriver driver, string database, HypergraphKernel kernel, byte fromRole, byte toRole)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));

        await session.ExecuteWriteAsync(async tx =>
        {
            foreach (var handle in kernel.VertexHandles())
            {
                await tx.RunAsync(
                    "MERGE (n:ToposVertex {handle: $handle})",
                    new { handle = (long)handle.Index });
            }

            foreach (var edgeHandle in ((IHypergraphQuery)kernel).HyperedgeHandles())
            {
                var members = kernel.GetHyperedgeVertices(edgeHandle);
                var sources = members.Where(m => m.Role == fromRole).Select(m => m.Member);
                var targets = members.Where(m => m.Role == toRole).Select(m => m.Member).ToList();

                foreach (var source in sources)
                {
                    foreach (var target in targets)
                    {
                        await tx.RunAsync(
                            """
                            MATCH (s:ToposVertex {handle: $s}), (t:ToposVertex {handle: $t})
                            MERGE (s)-[:ROLE_EDGE]->(t)
                            """,
                            new { s = (long)source.Index, t = (long)target.Index });
                    }
                }
            }
        });
    }

    private static async Task ClearAsync(IDriver driver, string database)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        await session.ExecuteWriteAsync(tx => tx.RunAsync("MATCH (n:ToposVertex) DETACH DELETE n"));
    }

    private static async Task<Dictionary<Handle, long>> RunGdsSccAsync(IDriver driver, string database)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        const string graphName = "toposDirectedSccParityGraph";
        await session.RunAsync("CALL gds.graph.drop($name, false)", new { name = graphName });
        await session.RunAsync(
            "CALL gds.graph.project($name, 'ToposVertex', 'ROLE_EDGE')",
            new { name = graphName });

        try
        {
            var cursor = await session.RunAsync(
                """
                CALL gds.scc.stream($name)
                YIELD nodeId, componentId
                RETURN gds.util.asNode(nodeId).handle AS handle, componentId
                """,
                new { name = graphName });

            var records = await cursor.ToListAsync();
            return records.ToDictionary(
                r => new Handle((uint)r["handle"].As<long>()),
                r => r["componentId"].As<long>());
        }
        finally
        {
            await session.RunAsync("CALL gds.graph.drop($name, false)", new { name = graphName });
        }
    }
}
