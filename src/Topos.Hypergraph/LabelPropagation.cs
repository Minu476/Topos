namespace Topos.Hypergraph;

/// <summary>
/// Label propagation community detection (spec §6 M6; GDS-verified via <c>gds.labelPropagation</c>
/// in <c>Topos.Tests.GdsOracle</c>). Iterative: each vertex adopts the most common label among
/// its <see cref="BipartiteAdjacency"/> neighbors, ties broken by lowest label for determinism.
/// Converges when no vertex changes label, or after <paramref name="maxIterations"/>.
///
/// <b>Chosen over Louvain, deliberately.</b> Louvain's multi-level modularity-optimization and
/// graph-aggregation process is a substantially more complex algorithm to get right from scratch
/// than this one — the same risk-avoidance call as the M0 CSR deferral, the M4 LSM-tree scope
/// note, and M5's brute-force (not true-ANN) vector index: don't build the harder, more
/// bug-prone version until a real workload's needs (here, community quality Label Propagation
/// can't reach) justify the extra complexity. Label Propagation gives real, GDS-verified
/// community detection today; Louvain is real follow-on work.
/// </summary>
public static class LabelPropagation
{
    public static IReadOnlyDictionary<Handle, int> DetectCommunities(IHypergraphQuery graph, int maxIterations = 100)
    {
        var vertices = graph.VertexHandles();
        var labels = new Dictionary<Handle, int>();
        for (int i = 0; i < vertices.Count; i++) labels[vertices[i]] = i;

        var order = new List<Handle>(vertices);
        var rng = new Random(Seed: 12345); // deterministic across runs, not a security concern

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            bool changed = false;
            Shuffle(order, rng);

            foreach (var v in order)
            {
                var neighborLabelCounts = new Dictionary<int, int>();
                foreach (var neighbor in BipartiteAdjacency.Neighbors(graph, v))
                    neighborLabelCounts[labels[neighbor]] = neighborLabelCounts.GetValueOrDefault(labels[neighbor]) + 1;

                if (neighborLabelCounts.Count == 0) continue; // isolated vertex keeps its own label

                int maxCount = neighborLabelCounts.Values.Max();
                int newLabel = neighborLabelCounts.Where(kv => kv.Value == maxCount).Min(kv => kv.Key);

                if (labels[v] != newLabel)
                {
                    labels[v] = newLabel;
                    changed = true;
                }
            }

            if (!changed) break;
        }

        return labels;
    }

    private static void Shuffle<T>(List<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
