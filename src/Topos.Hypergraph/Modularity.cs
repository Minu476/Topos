namespace Topos.Hypergraph;

/// <summary>
/// Newman's modularity Q for a given vertex partition (spec §6 M6) — scores how much more
/// densely connected the partition's communities are internally than expected by chance, over
/// the same topology-only bipartite adjacency every algorithm in this milestone shares. Not a
/// detection algorithm itself — scores a partition, e.g. <see cref="LabelPropagation"/>'s output.
///
/// Q = (internal edges / m) − Σ_c (degree sum of community c / 2m)² , the standard simplified
/// form of Newman's modularity for an unweighted, undirected graph.
/// </summary>
public static class Modularity
{
    public static double Compute(IHypergraphQuery graph, IReadOnlyDictionary<Handle, int> communities)
    {
        var edges = DistinctEdges(graph).ToList();
        int m = edges.Count;
        if (m == 0) return 0.0;

        var degree = new Dictionary<Handle, int>();
        foreach (var (a, b) in edges)
        {
            degree[a] = degree.GetValueOrDefault(a) + 1;
            degree[b] = degree.GetValueOrDefault(b) + 1;
        }

        double internalEdges = edges.Count(e =>
            communities.TryGetValue(e.A, out var ca) &&
            communities.TryGetValue(e.B, out var cb) &&
            ca == cb);

        double sumOfSquares = degree
            .GroupBy(kv => communities.GetValueOrDefault(kv.Key, -1))
            .Sum(group => Math.Pow(group.Sum(kv => (double)kv.Value), 2));

        return internalEdges / m - sumOfSquares / (4.0 * m * m);
    }

    private static IEnumerable<(Handle A, Handle B)> DistinctEdges(IHypergraphQuery graph)
    {
        var seen = new HashSet<(Handle, Handle)>();
        foreach (var v in graph.VertexHandles())
        {
            foreach (var n in BipartiteAdjacency.Neighbors(graph, v))
            {
                var edge = Canonical(v, n);
                if (seen.Add(edge)) yield return edge;
            }
        }
    }

    private static (Handle, Handle) Canonical(Handle a, Handle b) =>
        IsLessOrEqual(a, b) ? (a, b) : (b, a);

    private static bool IsLessOrEqual(Handle a, Handle b) =>
        a.Index < b.Index || (a.Index == b.Index && a.Generation <= b.Generation);
}
