namespace Topos.Hypergraph;

/// <summary>
/// Standard centrality measures over the topology-only bipartite adjacency M6's analytics family
/// shares (<see cref="BipartiteAdjacency"/> — the same co-membership-as-clique adjacency
/// <see cref="Modularity"/>/<see cref="TriangleCount"/>/<see cref="LabelPropagation"/> already use,
/// not M1's <see cref="IHypergraphQuery.GetBfs"/> member-only convention). Living here rather than
/// re-deriving M1's hop-collapsing adjacency means Centrality is GDS-verifiable with zero new
/// oracle machinery: <c>tests/Topos.Tests.GdsOracle</c>'s existing <c>CliqueExpansionProjectionEngine</c>
/// (already built for Modularity/TriangleCount/LabelPropagation) projects the identical adjacency,
/// so a straight <c>gds.degree</c>/<c>gds.closeness</c>/<c>gds.betweenness</c> comparison applies
/// with no doubled-hop-count correction (spec §5.5; provenance: yamafaktory/hypergraph's
/// <c>compute_centrality</c>, which bundles degree+closeness+betweenness in one pass).
/// </summary>
public static class Centrality
{
    /// <summary>
    /// Per-vertex degree: the size of its distinct neighbor set under <see cref="BipartiteAdjacency"/>.
    /// Matches <c>gds.degree.stream</c> over the clique-expansion projection with
    /// <c>orientation: 'UNDIRECTED'</c> — Neo4j's <c>MERGE</c>-based relationship creation already
    /// collapses duplicate pairs, so a distinct count on the Topos side is the fair comparison.
    /// </summary>
    public static IReadOnlyDictionary<Handle, int> Degree(IHypergraphQuery graph)
    {
        var result = new Dictionary<Handle, int>();
        foreach (var v in graph.VertexHandles())
            result[v] = new HashSet<Handle>(BipartiteAdjacency.Neighbors(graph, v)).Count;
        return result;
    }

    /// <summary>
    /// Closeness centrality: for each vertex, <c>k / Σ(distance to each of its k reachable others)</c>
    /// — 0.0 if nothing is reachable. This is GDS's actual default formula
    /// (<c>gds.closeness</c>'s <c>useWassermanFaustFormula</c> config defaults to <c>false</c>):
    /// each vertex is scored only against its own reachable set, not the whole graph's vertex
    /// count, so disconnected components each get an internally-consistent score rather than one
    /// artificially deflated by unreachable vertices elsewhere in the graph. Reduces to the classic
    /// Sabidussi <c>(n-1)/Σdistance</c> exactly when every vertex reaches every other (k = n-1 for
    /// all v). <see cref="CentralityTests"/> covers both a fully-connected case and two disjoint
    /// components scoring identically to prove the per-component (not global-n) behavior.
    /// </summary>
    public static IReadOnlyDictionary<Handle, double> Closeness(IHypergraphQuery graph)
    {
        var result = new Dictionary<Handle, double>();
        foreach (var v in graph.VertexHandles())
        {
            var distances = BfsDistances(graph, v);
            int k = distances.Count;
            if (k == 0) { result[v] = 0.0; continue; }
            long sum = distances.Values.Sum();
            result[v] = sum == 0 ? 0.0 : (double)k / sum;
        }
        return result;
    }

    /// <summary>
    /// Betweenness centrality: Brandes' algorithm (one BFS per source, O(V·(V+E))) over the same
    /// bipartite adjacency. Halved at the end — the standard undirected-graph correction, since
    /// Brandes' dependency accumulation discovers every shortest path twice (once from each
    /// endpoint as source).
    /// </summary>
    public static IReadOnlyDictionary<Handle, double> Betweenness(IHypergraphQuery graph)
    {
        var vertices = graph.VertexHandles();
        var betweenness = vertices.ToDictionary(v => v, _ => 0.0);

        foreach (var s in vertices)
        {
            var stack = new Stack<Handle>();
            var predecessors = vertices.ToDictionary(v => v, _ => new List<Handle>());
            var sigma = vertices.ToDictionary(v => v, _ => 0.0);
            var distance = vertices.ToDictionary(v => v, _ => -1);
            sigma[s] = 1.0;
            distance[s] = 0;

            var queue = new Queue<Handle>();
            queue.Enqueue(s);
            while (queue.Count > 0)
            {
                var v = queue.Dequeue();
                stack.Push(v);
                foreach (var w in new HashSet<Handle>(BipartiteAdjacency.Neighbors(graph, v)))
                {
                    if (distance[w] < 0)
                    {
                        distance[w] = distance[v] + 1;
                        queue.Enqueue(w);
                    }
                    if (distance[w] == distance[v] + 1)
                    {
                        sigma[w] += sigma[v];
                        predecessors[w].Add(v);
                    }
                }
            }

            var delta = vertices.ToDictionary(v => v, _ => 0.0);
            while (stack.Count > 0)
            {
                var w = stack.Pop();
                foreach (var v in predecessors[w])
                    delta[v] += sigma[v] / sigma[w] * (1 + delta[w]);
                if (w != s) betweenness[w] += delta[w];
            }
        }

        foreach (var v in vertices) betweenness[v] /= 2.0;
        return betweenness;
    }

    private static Dictionary<Handle, int> BfsDistances(IHypergraphQuery graph, Handle start)
    {
        var distances = new Dictionary<Handle, int> { [start] = 0 };
        var queue = new Queue<Handle>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in new HashSet<Handle>(BipartiteAdjacency.Neighbors(graph, current)))
            {
                if (!distances.ContainsKey(neighbor))
                {
                    distances[neighbor] = distances[current] + 1;
                    queue.Enqueue(neighbor);
                }
            }
        }
        distances.Remove(start);
        return distances;
    }
}
