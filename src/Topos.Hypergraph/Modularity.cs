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
    /// <summary>
    /// Newman's modularity Q for <paramref name="communities"/> over <paramref name="graph"/>'s
    /// bipartite adjacency. Returns 0.0 for an edgeless graph.
    ///
    /// <b>Vertices missing from <paramref name="communities"/> are handled two different ways —
    /// worth knowing if your partition doesn't cover every vertex.</b> For the internal-edges
    /// numerator, an edge counts as internal only if *both* endpoints have an explicit entry and
    /// they match — an edge touching an uncommunitied vertex is simply excluded, never counted as
    /// internal. For the degree sum-of-squares term, uncommunitied vertices are instead grouped
    /// together into one synthetic community (key <c>-1</c>) and *do* contribute to that penalty
    /// term. The net effect: leaving vertices out of <paramref name="communities"/> can only push
    /// Q down (their degree penalizes the score but their edges never count toward it), it never
    /// pushes it up. For a meaningful score, pass a <paramref name="communities"/> that covers
    /// every vertex <paramref name="graph"/> actually uses.
    /// </summary>
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
