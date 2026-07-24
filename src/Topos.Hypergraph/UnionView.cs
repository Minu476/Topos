namespace Topos.Hypergraph;

/// <summary>
/// A read-only, O(1)-to-construct view presenting the union of two <see cref="IHypergraphQuery"/>
/// sources — the one concept spec §6 M3 names in both its lists (JGraphT's composable views and
/// HyperNetX's set algebra), so it gets its own class rather than being folded into
/// <see cref="FilteredView"/> (which filters *one* source; this genuinely combines *two*).
///
/// <b>Conflict rule: <paramref name="a"/> wins.</b> If the same Handle resolves to a different
/// <see cref="Vertex"/> in both sources (e.g. different <see cref="VertexRoles"/>), the union
/// reports <paramref name="a"/>'s version. Worth stating plainly since it's not obvious from the
/// type signature.
///
/// <b>Only meaningful when both sources share a common Handle-identity space.</b> Two views
/// derived from the *same* <see cref="HypergraphKernel"/> (e.g. two <see cref="FilteredView"/>s
/// with different predicates) satisfy this trivially — a Handle means the same vertex in both.
/// Two *independently constructed* kernels do not: each has its own <see cref="HandleAllocator"/>
/// starting at Index 0, so <c>Handle(3)</c> in kernel A and <c>Handle(3)</c> in kernel B are
/// almost certainly unrelated vertices that happen to share a numeric coincidence, not the same
/// identity — unioning them would silently produce a meaningless result, not an error. See
/// <see cref="HypergraphViews"/>'s class doc for the version-diff technique that stays inside one
/// kernel's Handle-identity space on purpose.
/// </summary>
public sealed class UnionView(IHypergraphQuery a, IHypergraphQuery b) : IHypergraphQuery
{
    public int CountVertices() => VertexHandles().Count;

    public IReadOnlyList<Handle> VertexHandles() => [.. a.VertexHandles().Union(b.VertexHandles())];

    public bool TryGetVertex(Handle handle, out Vertex vertex) =>
        a.TryGetVertex(handle, out vertex) || b.TryGetVertex(handle, out vertex);

    public IReadOnlyList<Handle> GetVertexHyperedges(Handle vertex) =>
        [.. a.GetVertexHyperedges(vertex).Union(b.GetVertexHyperedges(vertex))];

    public IReadOnlyList<Incidence> GetHyperedgeVertices(Handle hyperedge) =>
        [.. a.GetHyperedgeVertices(hyperedge).Union(b.GetHyperedgeVertices(hyperedge))];
}
