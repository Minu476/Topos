namespace Topos.Hypergraph.Knowledge;

/// <summary>
/// Layer-1 role-aware directed traversal over <see cref="IHypergraphQuery"/> (spec §6 M9,
/// `docs/DECISIONS.md`'s 2026-07-25 "M9 SCOPED" entry). The kernel's own default algorithms
/// (<see cref="IHypergraphQuery.GetBfs"/> and friends) are deliberately role-blind and
/// co-membership-symmetric — "the kernel does not judge" (spec §4.1). This class is where that
/// judgment lives: given a directed reading of hyperedge roles (e.g. an Anchor role that fires
/// toward a Target role), walk only along that direction.
///
/// Generalizes a pattern three independent consumers already hand-rolled against nothing but
/// Topos's public API: Rich-Learning-Base's `ToposGraphProjection.DirectedBfs`/
/// `DirectedShortestPath` (hardcoded to Anchor=0/Target=2), NexusVerifier's
/// `CandidateEdgesFor`/`SubgoalsOf`, and `Topos.Samples.ChatMemory.EntitiesMentionedIn`. See
/// `docs/DECISIONS.md` for the full provenance and this package's exit criterion.
/// </summary>
public static class DirectedTraversal
{
    /// <summary>
    /// Breadth-first traversal from <paramref name="start"/>: follows only hyperedges where the
    /// current frontier vertex holds <paramref name="fromRole"/>, landing on that edge's
    /// <paramref name="toRole"/> members. Returns the visited set in discovery order, including
    /// <paramref name="start"/> itself.
    /// </summary>
    public static IReadOnlyList<Handle> DirectedBfs(
        this IHypergraphQuery graph, Handle start, byte fromRole, byte toRole)
    {
        var visited = new HashSet<Handle> { start };
        var queue = new Queue<Handle>();
        queue.Enqueue(start);
        var result = new List<Handle>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);

            foreach (var edge in graph.GetVertexHyperedges(current))
            {
                var members = graph.GetHyperedgeVertices(edge);
                if (!members.Any(m => m.Member == current && m.Role == fromRole)) continue;

                foreach (var m in members)
                {
                    if (m.Role == toRole && visited.Add(m.Member))
                        queue.Enqueue(m.Member);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// One shortest directed path from <paramref name="from"/> to <paramref name="to"/>, following
    /// only <paramref name="fromRole"/>→<paramref name="toRole"/> hyperedge legs — empty if
    /// unreachable, <c>[from]</c> if <paramref name="from"/> == <paramref name="to"/>. As with
    /// <see cref="IHypergraphQuery.GetShortestPath"/>, when multiple shortest paths tie, which one
    /// comes back depends on <see cref="IHypergraphQuery.GetHyperedgeVertices"/>'s iteration order.
    /// </summary>
    public static IReadOnlyList<Handle> DirectedShortestPath(
        this IHypergraphQuery graph, Handle from, Handle to, byte fromRole, byte toRole)
    {
        if (from == to) return [from];

        var parent = new Dictionary<Handle, Handle>();
        var queue = new Queue<Handle>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var edge in graph.GetVertexHyperedges(current))
            {
                var members = graph.GetHyperedgeVertices(edge);
                if (!members.Any(m => m.Member == current && m.Role == fromRole)) continue;

                foreach (var m in members)
                {
                    if (m.Role != toRole || m.Member == from || parent.ContainsKey(m.Member)) continue;
                    parent[m.Member] = current;
                    if (m.Member == to) return ReconstructPath(parent, from, to);
                    queue.Enqueue(m.Member);
                }
            }
        }
        return [];
    }

    /// <summary>
    /// The members of <paramref name="vertex"/>'s hyperedges that hold <paramref name="role"/> —
    /// the one-hop case. Generalizes `Topos.Samples.ChatMemory.EntitiesMentionedIn`'s hand-rolled
    /// <c>GetVertexHyperedges(v).SelectMany(...).Where(i =&gt; i.Role == X).Select(i =&gt; i.Member)</c>.
    /// </summary>
    public static IReadOnlyList<Handle> RoleFilteredMembers(
        this IHypergraphQuery graph, Handle vertex, byte role) =>
        [.. graph.GetVertexHyperedges(vertex)
            .SelectMany(graph.GetHyperedgeVertices)
            .Where(i => i.Role == role)
            .Select(i => i.Member)];

    private static IReadOnlyList<Handle> ReconstructPath(Dictionary<Handle, Handle> parent, Handle from, Handle to)
    {
        var path = new List<Handle> { to };
        var current = to;
        while (current != from)
        {
            current = parent[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }
}
