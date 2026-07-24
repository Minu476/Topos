namespace Topos.Hypergraph;

/// <summary>
/// Shared neighbor-gathering for M6's algorithms (LabelPropagation, TriangleCount, Modularity):
/// every vertex directly connected to <paramref name="v"/> across the full bipartite structure
/// (domain vertices and hyperedge-vertices alike), matching what <c>gds.wcc</c>/GDS-parity
/// operates on in <c>Topos.Tests.GdsOracle</c>. Same dual-direction pattern
/// <c>IHypergraphQuery.GetConnectedComponents</c> established in M1: check both "which edges is
/// <paramref name="v"/> a member of" and "if v is itself an edge, who are its members" — a plain
/// <c>GetBfs</c>-style traversal would silently skip edge-vertices, which is correct for
/// <c>GetBfs</c>'s own purpose but wrong here.
/// </summary>
internal static class BipartiteAdjacency
{
    public static IEnumerable<Handle> Neighbors(IHypergraphQuery graph, Handle v)
    {
        foreach (var edge in graph.GetVertexHyperedges(v))
        {
            yield return edge;
            foreach (var incidence in graph.GetHyperedgeVertices(edge))
            {
                if (incidence.Member != v) yield return incidence.Member;
            }
        }

        foreach (var incidence in graph.GetHyperedgeVertices(v))
        {
            yield return incidence.Member;
        }
    }
}
