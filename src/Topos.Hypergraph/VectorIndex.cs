namespace Topos.Hypergraph;

/// <summary>
/// Exact k-nearest-neighbor search over <c>PropertyKey&lt;float[]&gt;</c> embeddings (spec §6 M5:
/// "embeddings unified... ANN index as a separate derived structure").
///
/// <b>Brute-force, not approximate, despite the spec's "ANN" framing — deliberately.</b> A
/// correct O(V) linear scan is the honest baseline; a true approximate index (HNSW, IVF, LSH) is
/// a substantial, bug-prone undertaking on its own — the same caution as the M4 LSM-tree
/// deferral. Building one before a real workload's scale/latency needs justify it would be
/// exactly the speculative machinery this project's discipline avoids (same call as the M0 CSR
/// deferral: measure first). This class name says "VectorIndex," not "ApproximateNearestNeighborIndex,"
/// so it doesn't overclaim what it is.
///
/// It <i>is</i> the "derived structure" the spec names, though: built from the kernel's existing
/// <c>PropertyKey&lt;float[]&gt;</c> data via <see cref="HypergraphKernel.EnumerateProperty{T}"/>,
/// not stored inside the kernel itself — so swapping in a real ANN algorithm later touches only
/// this class, never <see cref="HypergraphKernel"/>.
/// </summary>
public sealed class VectorIndex(HypergraphKernel kernel, PropertyKey<float[]> embeddingKey)
{
    /// <summary>
    /// The <paramref name="k"/> nearest vertices to <paramref name="query"/> by squared Euclidean
    /// distance, ascending (nearest first). Throws <see cref="ArgumentOutOfRangeException"/> if
    /// <paramref name="k"/> isn't positive, and <see cref="ArgumentException"/> if
    /// <paramref name="query"/>'s length doesn't match a stored embedding's length — this class
    /// doesn't pad/truncate mismatched dimensions, since that would silently produce a meaningless
    /// distance rather than surface the caller's error.
    /// </summary>
    public IReadOnlyList<(Handle Handle, float Distance)> NearestNeighbors(ReadOnlySpan<float> query, int k)
    {
        if (k <= 0) throw new ArgumentOutOfRangeException(nameof(k), "k must be positive.");

        var queryArray = query.ToArray(); // captured for the sort delegate below
        var candidates = new List<(Handle Handle, float Distance)>();
        foreach (var (handle, embedding) in kernel.EnumerateProperty(embeddingKey))
            candidates.Add((handle, SquaredEuclideanDistance(queryArray, embedding)));

        candidates.Sort(static (a, b) => a.Distance.CompareTo(b.Distance));
        return candidates.Count <= k ? candidates : candidates.GetRange(0, k);
    }

    private static float SquaredEuclideanDistance(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException($"Embedding dimension mismatch: query has {a.Length}, stored vector has {b.Length}.");

        float sum = 0;
        for (int i = 0; i < a.Length; i++)
        {
            float d = a[i] - b[i];
            sum += d * d;
        }
        return sum;
    }
}
