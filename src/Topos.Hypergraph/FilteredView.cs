namespace Topos.Hypergraph;

/// <summary>
/// A read-only, O(1)-to-construct view over an <see cref="IHypergraphQuery"/> source, restricted
/// to vertices passing <paramref name="predicate"/> — spec §6 M3's "subgraph" and "mask" views
/// (JGraphT pattern) unified as one mechanism, since a fixed vertex-set subgraph and a live
/// masking predicate are the same operation at different points on one spectrum (a
/// <c>HashSet&lt;Handle&gt;.Contains</c> predicate gives you the "fixed subgraph" case; anything
/// else gives you a live mask). No separate classes for the two — same class, different
/// predicate.
///
/// A hyperedge is included only when the predicate accepts the edge-vertex itself; a member is
/// reported only when the predicate also accepts that member — mirroring JGraphT's
/// <c>AsSubgraph</c> convention (an edge counts only if both its endpoints are in the vertex
/// subset), generalized to N-ary: a member outside the view is silently dropped from that edge's
/// reported membership, not an error.
///
/// Every algorithm on <see cref="IHypergraphQuery"/> (BFS, DFS, shortest path, cycle detection,
/// transitive closure, connected components) works correctly over a <see cref="FilteredView"/>
/// with zero additional code — they're all default-implemented purely from the 5 primitives this
/// class re-implements, which is the entire point of the trait pattern spec §6 M1 borrowed from
/// yamafaktory.
/// </summary>
public sealed class FilteredView(IHypergraphQuery source, Func<Handle, bool> predicate) : IHypergraphQuery
{
    public int CountVertices() => VertexHandles().Count;

    public IReadOnlyList<Handle> VertexHandles() => [.. source.VertexHandles().Where(predicate)];

    public bool TryGetVertex(Handle handle, out Vertex vertex)
    {
        if (predicate(handle) && source.TryGetVertex(handle, out vertex)) return true;
        vertex = new Vertex(Handle.Invalid, VertexRoles.None, VertexStatus.Dormant);
        return false;
    }

    public IReadOnlyList<Handle> GetVertexHyperedges(Handle vertex) =>
        predicate(vertex) ? [.. source.GetVertexHyperedges(vertex).Where(predicate)] : [];

    public IReadOnlyList<Incidence> GetHyperedgeVertices(Handle hyperedge) =>
        predicate(hyperedge) ? [.. source.GetHyperedgeVertices(hyperedge).Where(i => predicate(i.Member))] : [];
}
