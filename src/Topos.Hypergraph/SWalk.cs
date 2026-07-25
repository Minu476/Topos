namespace Topos.Hypergraph;

/// <summary>
/// s-walk / s-distance traversal over hyperedges (spec §6 M6, §7 pattern 17 — HyperNetX/Julia).
/// Two hyperedges are <b>s-adjacent</b> when they share at least <c>s</c> common members; an
/// s-walk is a path of s-adjacent hyperedges; s-distance is the length of the shortest one.
///
/// <b>The one genuinely hypergraph-specific algorithm in this milestone — deliberately not
/// GDS-verified.</b> GDS operates on the binary-graph projection and has no notion of "these two
/// hyperedges share at least s common members" (spec §5's own honest limit: "where the
/// hypergraph and its projection disagree, GDS cannot verify... Topos's answer is the novel
/// claim there"). No <c>Topos.Tests.GdsOracle</c> test exists for this for that reason, not an
/// oversight — see <c>SWalkTests</c> for hand-verified correctness instead.
/// </summary>
public static class SWalk
{
    /// <summary>
    /// Every hyperedge reachable from <paramref name="start"/> via a chain of s-adjacent
    /// hyperedges. Throws <see cref="ArgumentOutOfRangeException"/> immediately (not deferred to
    /// first enumeration) if <paramref name="s"/> is less than 1 — matching <see cref="Distance"/>'s
    /// eager-throw behavior. This method is implemented as an iterator internally, but the guard
    /// clause is split into this eager wrapper deliberately: a bare <c>yield return</c> method's
    /// argument checks don't run until the caller starts enumerating, which would make an invalid
    /// <paramref name="s"/> throw only on <c>.ToList()</c>/<c>foreach</c>, not on the call itself
    /// — a real footgun for a caller who builds the sequence without immediately consuming it.
    /// </summary>
    public static IEnumerable<Handle> Reachable(IHypergraphQuery graph, Handle start, int s)
    {
        if (s < 1) throw new ArgumentOutOfRangeException(nameof(s), "s must be at least 1.");
        return ReachableCore(graph, start, s);
    }

    private static IEnumerable<Handle> ReachableCore(IHypergraphQuery graph, Handle start, int s)
    {
        if (!graph.ContainsVertex(start)) yield break;

        var edges = graph.HyperedgeHandles();
        var visited = new HashSet<Handle> { start };
        var queue = new Queue<Handle>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;
            var currentMembers = MemberSet(graph, current);

            foreach (var candidate in edges)
            {
                if (visited.Contains(candidate)) continue;
                if (SharedCount(currentMembers, MemberSet(graph, candidate)) >= s)
                {
                    visited.Add(candidate);
                    queue.Enqueue(candidate);
                }
            }
        }
    }

    /// <summary>
    /// Shortest s-walk length between two hyperedges, or null if none exists at this s. Throws
    /// <see cref="ArgumentOutOfRangeException"/> immediately if <paramref name="s"/> is less than 1.
    /// </summary>
    public static int? Distance(IHypergraphQuery graph, Handle from, Handle to, int s)
    {
        if (s < 1) throw new ArgumentOutOfRangeException(nameof(s), "s must be at least 1.");
        if (!graph.ContainsVertex(from) || !graph.ContainsVertex(to)) return null;
        if (from == to) return 0;

        var edges = graph.HyperedgeHandles();
        var visited = new HashSet<Handle> { from };
        var queue = new Queue<(Handle Edge, int Distance)>();
        queue.Enqueue((from, 0));

        while (queue.Count > 0)
        {
            var (current, distance) = queue.Dequeue();
            var currentMembers = MemberSet(graph, current);

            foreach (var candidate in edges)
            {
                if (visited.Contains(candidate)) continue;
                if (SharedCount(currentMembers, MemberSet(graph, candidate)) < s) continue;

                if (candidate == to) return distance + 1;
                visited.Add(candidate);
                queue.Enqueue((candidate, distance + 1));
            }
        }
        return null;
    }

    private static HashSet<Handle> MemberSet(IHypergraphQuery graph, Handle edge) =>
        [.. graph.GetHyperedgeVertices(edge).Select(i => i.Member)];

    private static int SharedCount(HashSet<Handle> a, HashSet<Handle> b)
    {
        int count = 0;
        foreach (var h in a)
        {
            if (b.Contains(h)) count++;
        }
        return count;
    }
}
