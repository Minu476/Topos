using Neo4j.Driver;
using Topos.Hypergraph;

namespace Topos.Tests.GdsOracle;

/// <summary>
/// Projects a Topos hypergraph into Neo4j using clique expansion: for every hyperedge, besides
/// the bipartite edge-to-member relationships, also adds a direct relationship between every
/// pair of its members. This matches what <c>BipartiteAdjacency.Neighbors</c> (used by
/// <c>TriangleCount</c>, <c>LabelPropagation</c>, <c>Modularity</c>) actually computes —
/// co-members are treated as directly adjacent to each other, not merely adjacent-via-the-edge.
///
/// <b>Found necessary by a failing GDS-parity test, not designed in up front.</b> The plain
/// bipartite <see cref="ProjectionEngine"/> is correct for BFS/WCC, which only care about
/// reachability and are hop-count-insensitive — a-edge-b being a 2-hop path instead of a direct
/// edge doesn't change which component something is in. It's wrong for triangle counting: a
/// 2-member hyperedge's bipartite projection has no a-b edge at all, so GDS reported 0 triangles
/// against Topos's <c>TriangleCount</c> reporting 1 (see that class's own doc comment for why 1
/// is correct). Clique expansion is the standard, named hypergraph-to-graph transformation for
/// exactly this reason — not an ad hoc fix invented for this test.
///
/// Uses an <c>:ADJACENT</c> relationship type, deliberately distinct from <see cref="ProjectionEngine"/>'s
/// <c>:INCIDENT</c> — this relationship doesn't carry role/ordinal (it's synthesized structure,
/// not a literal <see cref="Incidence"/> record), and keeping the types separate means both
/// projections can coexist in the same database without interfering.
/// </summary>
internal static class CliqueExpansionProjectionEngine
{
    public static async Task ProjectAsync(IDriver driver, string database, HypergraphKernel kernel)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));

        await session.ExecuteWriteAsync(async tx =>
        {
            foreach (var handle in kernel.VertexHandles())
            {
                kernel.TryGetVertex(handle, out var vertex);
                await tx.RunAsync(
                    "MERGE (n:ToposVertex {handle: $handle}) SET n.isEdge = $isEdge",
                    new { handle = (long)handle.Index, isEdge = vertex.Roles.HasFlag(VertexRoles.Edge) });
            }

            foreach (var edgeHandle in ((IHypergraphQuery)kernel).HyperedgeHandles())
            {
                var members = kernel.IncidencesFrom(edgeHandle).Select(i => i.Member).ToList();

                foreach (var member in members)
                {
                    await tx.RunAsync(
                        """
                        MATCH (s:ToposVertex {handle: $s}), (m:ToposVertex {handle: $m})
                        MERGE (s)-[:ADJACENT]->(m)
                        """,
                        new { s = (long)edgeHandle.Index, m = (long)member.Index });
                }

                for (int i = 0; i < members.Count; i++)
                {
                    for (int j = i + 1; j < members.Count; j++)
                    {
                        await tx.RunAsync(
                            """
                            MATCH (a:ToposVertex {handle: $a}), (b:ToposVertex {handle: $b})
                            MERGE (a)-[:ADJACENT]->(b)
                            """,
                            new { a = (long)members[i].Index, b = (long)members[j].Index });
                    }
                }
            }
        });
    }

    public static async Task ClearAsync(IDriver driver, string database)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        await session.ExecuteWriteAsync(tx => tx.RunAsync("MATCH (n:ToposVertex) DETACH DELETE n"));
    }
}
