using Neo4j.Driver;
using Topos.Hypergraph;

namespace Topos.Tests.GdsOracle;

/// <summary>
/// Projects a Topos hypergraph into Neo4j as a bipartite property graph (spec §5's
/// "ProjectionEngine" — the mechanism that gives GDS a binary-graph view it can operate on).
///
/// The projection is close to a direct translation, not a lossy expansion algorithm, because
/// Topos's own storage model is already reification-based (spec §7 pattern 12: a hyperedge
/// *is* a Vertex tagged <see cref="VertexRoles.Edge"/>, connected to its members via
/// <see cref="Incidence"/> records). So: every Topos <see cref="Vertex"/> becomes one
/// <c>:ToposVertex</c> node (tagged <c>isEdge</c> for hyperedge-vertices), and every
/// <see cref="Incidence"/> becomes one directed <c>:INCIDENT</c> relationship
/// Source→Member carrying <c>role</c>/<c>ordinal</c>.
///
/// <b>What this does and doesn't verify.</b> A GDS algorithm run over this bipartite projection
/// walks through edge-nodes as ordinary hops — it doesn't know Topos's convention that
/// edge-vertices are traversal scaffolding, not domain results (spec §4.1: "the kernel does not
/// judge"). Callers comparing GDS output against Topos's own <c>IHypergraphQuery</c> defaults
/// (e.g. <see cref="HypergraphKernel.GetBfs"/>, which reports only domain vertices reached, per
/// the class comment above) need to filter GDS's raw result to non-edge nodes for a fair
/// comparison — see <c>BfsGdsParityTests</c> for exactly that filter. This is the precise
/// "hypergraph vs. its projection" seam spec §5 names as GDS's honest limit.
///
/// <b>Orientation note (found by direct debugging, not obvious up front):</b> the
/// <c>:INCIDENT</c> relationship is stored directed, Source→Member, matching Topos's own
/// storage. But <see cref="HypergraphKernel.GetBfs"/> (the algorithm this projection exists to
/// verify) treats hyperedge co-membership as *symmetric* — it looks up "which edges is this
/// vertex a member of" (against the stored direction) and then "who else is on that edge" (with
/// it). A caller projecting this graph into GDS for a BFS/DFS-style comparison must project
/// <c>INCIDENT</c> with <c>orientation: 'UNDIRECTED'</c>, or GDS's traversal will dead-end at any
/// pure-member start node (one with no outgoing INCIDENT edge). See
/// <c>BfsGdsParityTests.RunGdsBfsAsync</c> for where this matters.
/// </summary>
internal static class ProjectionEngine
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

            foreach (var handle in kernel.VertexHandles())
            {
                foreach (var incidence in kernel.IncidencesFrom(handle))
                {
                    await tx.RunAsync(
                        """
                        MATCH (s:ToposVertex {handle: $source}), (m:ToposVertex {handle: $member})
                        MERGE (s)-[:INCIDENT {role: $role, ordinal: $ordinal}]->(m)
                        """,
                        new
                        {
                            source = (long)incidence.Source.Index,
                            member = (long)incidence.Member.Index,
                            role = (long)incidence.Role,
                            ordinal = (long)incidence.Ordinal,
                        });
                }
            }
        });
    }

    /// <summary>Wipes every <c>:ToposVertex</c> node this harness created — run before and after each test so runs don't accumulate state in the (otherwise-empty, throwaway) oracle database.</summary>
    public static async Task ClearAsync(IDriver driver, string database)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        await session.ExecuteWriteAsync(tx => tx.RunAsync("MATCH (n:ToposVertex) DETACH DELETE n"));
    }
}
