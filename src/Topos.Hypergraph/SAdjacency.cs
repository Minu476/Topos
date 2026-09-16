namespace Topos.Hypergraph;

/// <summary>
/// The shared derivation behind M11 phase 2's s-connectivity family (`docs/ALGORITHM_SPEC.md`
/// §5.1-5.3): turn the incidence structure into a plain binary adjacency in which two nodes are
/// linked iff they share at least <c>s</c> of the other side's elements, then run ordinary graph
/// algorithms over it. Both <see cref="SDomain"/> readings are the same routine with the two sides
/// swapped, which is why they live in one place rather than as two near-copies.
///
/// <b>Why co-occurrence counting rather than pairwise set intersection.</b> The spec's §5.1 note
/// budgets O(V²·E) for "count shared hyperedges for every vertex pair." That is honest but
/// needlessly pessimistic: every shared element is *already* a list of the nodes that share it, so
/// one pass over those lists accumulates every pairwise share-count directly. The cost becomes
/// Σ|group|² over the groups that actually exist (Σ_e |members(e)|² for <see cref="SDomain.Vertices"/>,
/// Σ_v |hyperedges(v)|² for <see cref="SDomain.Hyperedges"/>) instead of N² × group-count, and it
/// never touches a pair that shares nothing. Same answer, strictly less work, no extra storage and
/// no new index — so this isn't the kind of optimization the M0 benchmark gate exists to hold back
/// (there's no complexity being bought on speculation here; the simpler-to-state bound is simply
/// also the slower one).
/// </summary>
internal static class SAdjacency
{
    /// <summary>
    /// The node set for <paramref name="domain"/>, in handle-allocation order so every downstream
    /// result (component order, component contents) is deterministic across runs.
    /// </summary>
    public static IReadOnlyList<Handle> Nodes(IHypergraphQuery graph, SDomain domain)
    {
        if (domain == SDomain.Hyperedges) return graph.HyperedgeHandles();

        // SDomain.Vertices: every handle that is a member of at least one hyperedge — HNX's node
        // set. Deliberately not every handle: see SDomain.Vertices' own doc for why a
        // membership-less handle is absent rather than a singleton.
        var result = new List<Handle>();
        foreach (var h in graph.VertexHandles())
        {
            if (graph.GetVertexHyperedges(h).Count > 0) result.Add(h);
        }
        return result;
    }

    /// <summary>
    /// The elements <paramref name="node"/> shares with its peers — the hyperedges it belongs to
    /// (<see cref="SDomain.Vertices"/>) or the vertices it contains (<see cref="SDomain.Hyperedges"/>).
    /// Deduplicated: a vertex may legitimately appear in one hyperedge under several ordinals or
    /// roles (an ordered n-ary relation with a repeated participant), and counting that twice would
    /// inflate the share-count and fabricate s-adjacency that the definition does not license.
    /// </summary>
    private static HashSet<Handle> SharedElements(IHypergraphQuery graph, Handle node, SDomain domain) =>
        domain == SDomain.Vertices
            ? [.. graph.GetVertexHyperedges(node)]
            : [.. graph.GetHyperedgeVertices(node).Select(i => i.Member)];

    /// <summary>
    /// The s-adjacency: node → its s-adjacent neighbors, every node present (isolated ones map to
    /// an empty list). Neighbor lists are in the node set's own order. Throws for <c>s &lt; 1</c>,
    /// matching <see cref="SWalk"/>'s eager-throw convention.
    /// </summary>
    public static Dictionary<Handle, List<Handle>> Build(IHypergraphQuery graph, int s, SDomain domain)
    {
        if (s < 1) throw new ArgumentOutOfRangeException(nameof(s), "s must be at least 1.");

        var nodes = Nodes(graph, domain);
        var index = new Dictionary<Handle, int>(nodes.Count);
        for (int i = 0; i < nodes.Count; i++) index[nodes[i]] = i;

        // Walk the *other* side's elements; each one is a group of nodes that share it, so one
        // pass over the groups accumulates every pairwise share-count. Vertices co-occur *in a
        // hyperedge*; hyperedges co-occur *at a vertex* — the same loop, sides swapped.
        var other = domain == SDomain.Vertices ? SDomain.Hyperedges : SDomain.Vertices;
        var shareCount = new Dictionary<(int Low, int High), int>();

        foreach (var group in Nodes(graph, other))
        {
            var members = new List<int>();
            foreach (var member in SharedElements(graph, group, other))
            {
                if (index.TryGetValue(member, out int i)) members.Add(i);
            }
            if (members.Count < 2) continue;

            members.Sort();
            for (int i = 0; i < members.Count; i++)
            {
                for (int j = i + 1; j < members.Count; j++)
                {
                    var key = (members[i], members[j]);
                    shareCount[key] = shareCount.TryGetValue(key, out int c) ? c + 1 : 1;
                }
            }
        }

        var adjacency = new Dictionary<Handle, List<Handle>>(nodes.Count);
        foreach (var n in nodes) adjacency[n] = [];
        foreach (var pair in shareCount)
        {
            if (pair.Value < s) continue;
            adjacency[nodes[pair.Key.Low]].Add(nodes[pair.Key.High]);
            adjacency[nodes[pair.Key.High]].Add(nodes[pair.Key.Low]);
        }
        foreach (var n in nodes) adjacency[n].Sort((x, y) => index[x].CompareTo(index[y]));
        return adjacency;
    }

    /// <summary>
    /// Connected components of a prebuilt s-adjacency, in node-set order, each component's members
    /// in node-set order. Singleton components are included — the information "this node is
    /// s-isolated at this s" is exactly what distinguishes one s from the next, and HNX's own
    /// <c>return_singletons=True</c> mode is the comparable one.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<Handle>> Components(
        IReadOnlyList<Handle> nodes, Dictionary<Handle, List<Handle>> adjacency)
    {
        var components = new List<IReadOnlyList<Handle>>();
        var visited = new HashSet<Handle>();

        foreach (var start in nodes)
        {
            if (!visited.Add(start)) continue;

            var component = new List<Handle>();
            var queue = new Queue<Handle>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);
                foreach (var next in adjacency[current])
                {
                    if (visited.Add(next)) queue.Enqueue(next);
                }
            }
            components.Add(component);
        }
        return components;
    }

    /// <summary>
    /// Hop distances from <paramref name="start"/> over a prebuilt s-adjacency, excluding
    /// <paramref name="start"/> itself. One hop is one s-adjacency step.
    /// </summary>
    public static Dictionary<Handle, int> Distances(Handle start, Dictionary<Handle, List<Handle>> adjacency)
    {
        var distances = new Dictionary<Handle, int>();
        if (!adjacency.ContainsKey(start)) return distances;

        var seen = new HashSet<Handle> { start };
        var queue = new Queue<(Handle Node, int Distance)>();
        queue.Enqueue((start, 0));

        while (queue.Count > 0)
        {
            var (current, distance) = queue.Dequeue();
            if (current != start) distances[current] = distance;
            foreach (var next in adjacency[current])
            {
                if (seen.Add(next)) queue.Enqueue((next, distance + 1));
            }
        }
        return distances;
    }
}
