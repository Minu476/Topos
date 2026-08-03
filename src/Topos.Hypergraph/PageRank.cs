namespace Topos.Hypergraph;

/// <summary>
/// PageRank over the same topology-only bipartite adjacency <see cref="Centrality"/>/
/// <see cref="Modularity"/>/<see cref="TriangleCount"/> share (<see cref="BipartiteAdjacency"/>) —
/// symmetric co-membership, kernel-level (spec §5.6, §6.3 open fork: "recommend symmetric,
/// kernel-level for v1"; a directed variant can follow in <c>Topos.Hypergraph.Knowledge</c> if a
/// consumer needs it). Standard power iteration with uniform dangling-mass redistribution, to a
/// fixed tolerance or iteration cap. GDS-verified (<c>gds.pageRank</c> over the
/// <c>CliqueExpansionProjectionEngine</c> graph, UNDIRECTED) — reuses the same oracle machinery
/// <see cref="Centrality"/> does, no new projection needed.
/// </summary>
public static class PageRank
{
    /// <summary>
    /// Per-vertex PageRank score, converging to a distribution that sums to 1.0 across every
    /// vertex regardless of <paramref name="damping"/> or dangling-node mass (a graph with zero
    /// vertices returns an empty result rather than dividing by zero).
    /// </summary>
    public static IReadOnlyDictionary<Handle, double> Compute(
        IHypergraphQuery graph, double damping = 0.85, int maxIterations = 100, double tolerance = 1e-6)
    {
        var vertices = graph.VertexHandles();
        int n = vertices.Count;
        var rank = new Dictionary<Handle, double>();
        if (n == 0) return rank;

        var neighbors = new Dictionary<Handle, List<Handle>>();
        foreach (var v in vertices)
        {
            neighbors[v] = [.. new HashSet<Handle>(BipartiteAdjacency.Neighbors(graph, v))];
            rank[v] = 1.0 / n;
        }

        for (int iter = 0; iter < maxIterations; iter++)
        {
            double danglingMass = vertices.Where(v => neighbors[v].Count == 0).Sum(v => rank[v]);
            double baseRank = (1.0 - damping) / n + damping * danglingMass / n;

            var next = vertices.ToDictionary(v => v, _ => baseRank);
            foreach (var u in vertices)
            {
                int outDegree = neighbors[u].Count;
                if (outDegree == 0) continue;
                double share = damping * rank[u] / outDegree;
                foreach (var v in neighbors[u]) next[v] += share;
            }

            double delta = vertices.Sum(v => Math.Abs(next[v] - rank[v]));
            rank = next;
            if (delta < tolerance) break;
        }

        return rank;
    }
}
