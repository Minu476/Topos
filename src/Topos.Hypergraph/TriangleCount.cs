namespace Topos.Hypergraph;

/// <summary>
/// Triangle counting over the topology-only bipartite adjacency (spec §6 M6; GDS-verified via
/// <c>gds.triangleCount</c> in <c>Topos.Tests.GdsOracle</c>). A triangle is three distinct
/// vertices, each pair directly connected via <see cref="BipartiteAdjacency"/>.
///
/// Standard "ordered neighbor pairs" method: for each vertex, only its neighbors with a strictly
/// higher <see cref="Handle"/> order are considered, and edges among *those* pairs are counted —
/// this counts each triangle exactly once (when processing its lowest-ordered member) rather
/// than three times (once per member), with no separate de-duplication pass needed.
///
/// <b>Non-obvious consequence of the bipartite reification (same family of finding as
/// <c>IHypergraphQuery.HasCycle</c>'s doc comment in M1):</b> a single hyperedge with N members
/// is directly connected to every one of them, *and* those members are already pairwise adjacent
/// to each other (the same co-membership convention <c>GetBfs</c> uses) — so an N-member
/// hyperedge and its members together form a complete graph on N+1 vertices, giving
/// <c>C(N+1, 3)</c> triangles from *one* hyperedge. This holds even at the minimum: a plain
/// 2-member (binary) hyperedge already forms a triangle with its own edge-vertex — <c>C(3,3) = 1</c>
/// — found by a failing test that assumed otherwise (<c>TriangleCountTests</c>' original
/// "no triangle" expectation for a 2-member edge was simply wrong). A 3-member hyperedge (RLB's
/// minimal D2 shape with one Condition) produces <c>C(4,3) = 4</c> triangles.
/// </summary>
public static class TriangleCount
{
    public static long Count(IHypergraphQuery graph)
    {
        var vertices = graph.VertexHandles();
        var adjacency = new Dictionary<Handle, HashSet<Handle>>();
        foreach (var v in vertices) adjacency[v] = [.. BipartiteAdjacency.Neighbors(graph, v)];

        long triangles = 0;
        foreach (var v in vertices)
        {
            var higherNeighbors = adjacency[v].Where(n => IsGreater(n, v)).ToList();
            for (int i = 0; i < higherNeighbors.Count; i++)
            {
                for (int j = i + 1; j < higherNeighbors.Count; j++)
                {
                    if (adjacency[higherNeighbors[i]].Contains(higherNeighbors[j]))
                        triangles++;
                }
            }
        }
        return triangles;
    }

    private static bool IsGreater(Handle a, Handle b) =>
        a.Index > b.Index || (a.Index == b.Index && a.Generation > b.Generation);
}
