namespace Topos.Hypergraph;

/// <summary>
/// M11 phase 2 — the s-connectivity family (`docs/ALGORITHM_SPEC.md` §5.1-5.3): s-connected
/// components, the s-line-graph, and s-distance/eccentricity/diameter.
///
/// <b>Novel-by-nature, and verified accordingly.</b> Neo4j GDS structurally cannot check any of
/// this — "these two hyperedges share at least s common members" has no meaning in a binary
/// projection (spec §5's own honest limit, the same one <see cref="SWalk"/> already documents). The
/// verification here is `ALGORITHM_GAP_LIST.md` §3-4's ladder instead of a single oracle:
/// T2 differential parity against HyperNetX and xgi (`tests/Topos.Tests.HnxOracle`), T3
/// metamorphic properties, T4 hand-computed golden cases, and T5 reduction anchors that pin s=1
/// onto the already-GDS-verified <see cref="IHypergraphQuery.GetConnectedComponents"/> and onto
/// <see cref="SWalk"/>.
///
/// Every method here is read-only over the existing primitives and adds no storage (spec §8): the
/// s-adjacency is derived on each call by <see cref="SAdjacency"/>, never cached. A caller running
/// several of these at one <c>s</c> pays that derivation each time — fine at correctness-first
/// scope, and a caching overload is exactly the kind of thing the M0 benchmark gate says to add
/// against a measured workload rather than on spec.
/// </summary>
public partial interface IHypergraphQuery
{
    /// <summary>
    /// Partitions the <paramref name="domain"/>'s nodes into s-connected components: two nodes
    /// share a component when a chain of s-adjacent nodes links them. Defaults to
    /// <see cref="SDomain.Vertices"/>, matching spec §5.1's own signature — at <c>s</c>=1 that
    /// reduces to <see cref="GetConnectedComponents"/> restricted to member vertices (the T5
    /// anchor; <c>SConnectedComponentsTests</c> asserts the reduction directly, and the
    /// restriction is not a caveat bolted on after the fact — <see cref="GetConnectedComponents"/>
    /// partitions the bipartite structure, so its components additionally contain the
    /// hyperedge-vertices that this vertex-domain view has no node for).
    ///
    /// Components and their members come back in handle-allocation order, so results are stable
    /// run to run. Singletons are included (see <see cref="SAdjacency.Components"/>); HNX's
    /// comparable mode is <c>return_singletons=True</c>, since its default drops them.
    /// Throws <see cref="ArgumentOutOfRangeException"/> eagerly for <c>s</c> &lt; 1, matching
    /// <see cref="SWalk"/>.
    /// </summary>
    IReadOnlyList<IReadOnlyList<Handle>> GetSConnectedComponents(int s = 1, SDomain domain = SDomain.Vertices)
    {
        var nodes = SAdjacency.Nodes(this, domain);
        return SAdjacency.Components(nodes, SAdjacency.Build(this, s, domain));
    }

    /// <summary>
    /// The s-line-graph: a plain binary adjacency (node → its s-adjacent neighbors) over the
    /// <paramref name="domain"/>'s nodes. Defaults to <see cref="SDomain.Hyperedges"/> — spec
    /// §5.2 defines this one on hyperedges ("nodes are this hypergraph's hyperedges, linked iff
    /// they share ≥ s member vertices"), which is why its default differs from its two siblings
    /// here; each method's default is the domain its own spec section is written against.
    ///
    /// Every node appears as a key, including nodes with no s-adjacent neighbor at this <c>s</c>
    /// (an empty list, not an absent key) — HNX's <c>get_linegraph</c> and xgi's
    /// <c>to_line_graph</c> both keep such a node in the graph as an isolate, and dropping it here
    /// would silently disagree with both oracles about the node set.
    ///
    /// <see cref="SDomain.Vertices"/> gives the dual — the vertex s-adjacency the other two
    /// methods run over, which HNX exposes as <c>get_linegraph(edges=False)</c>. Exposing it is
    /// what lets the oracle project verify the derived adjacency itself, not merely the components
    /// computed from it.
    /// </summary>
    IReadOnlyDictionary<Handle, IReadOnlyList<Handle>> GetSLineGraph(int s = 1, SDomain domain = SDomain.Hyperedges)
    {
        var adjacency = SAdjacency.Build(this, s, domain);
        var result = new Dictionary<Handle, IReadOnlyList<Handle>>(adjacency.Count);
        foreach (var pair in adjacency) result[pair.Key] = pair.Value;
        return result;
    }

    /// <summary>
    /// Shortest s-distance between two nodes of <paramref name="domain"/> — the number of
    /// s-adjacency steps — or null if no s-walk connects them (including when either handle is not
    /// a node of this domain). 0 when <paramref name="from"/> == <paramref name="to"/>, provided
    /// that handle is a node here.
    ///
    /// At <see cref="SDomain.Hyperedges"/> this is <see cref="SWalk.Distance"/>'s answer by
    /// construction — the two are cross-checked against each other as a T5 anchor rather than one
    /// delegating to the other, so a regression in either implementation surfaces as a
    /// disagreement instead of being inherited silently. HNX's <c>distance</c>/<c>edge_distance</c>
    /// are the external oracle; note HNX reports an unreachable pair as <c>inf</c> where this
    /// returns null.
    /// </summary>
    int? GetSDistance(Handle from, Handle to, int s = 1, SDomain domain = SDomain.Vertices)
    {
        var adjacency = SAdjacency.Build(this, s, domain);
        if (!adjacency.ContainsKey(from) || !adjacency.ContainsKey(to)) return null;
        if (from == to) return 0;
        return SAdjacency.Distances(from, adjacency).TryGetValue(to, out int d) ? d : null;
    }

    /// <summary>
    /// The s-eccentricity of one node: the greatest s-distance from it to any node it can reach.
    /// 0 for an s-isolated node (the standard graph-theoretic reading, and the only value
    /// consistent with "greatest distance to anything reachable" when nothing is). Null when
    /// <paramref name="of"/> is not a node of <paramref name="domain"/> at all.
    ///
    /// <b>Diverges from HNX deliberately, on disconnected inputs.</b> HNX's <c>s_eccentricity</c>
    /// raises rather than answering when the hypergraph is not globally s-connected, because it
    /// defers to NetworkX's whole-graph eccentricity. This measures within the node's own
    /// s-component instead, which stays defined for every input. The two agree exactly wherever
    /// HNX is willing to answer, and `tests/Topos.Tests.HnxOracle` asserts both halves of that:
    /// parity on s-connected fixtures, and the divergence itself on a disconnected one, so this is
    /// a recorded difference rather than an undetected one.
    /// </summary>
    int? GetSEccentricity(Handle of, int s = 1, SDomain domain = SDomain.Vertices)
    {
        var adjacency = SAdjacency.Build(this, s, domain);
        if (!adjacency.ContainsKey(of)) return null;
        var distances = SAdjacency.Distances(of, adjacency);
        return distances.Count == 0 ? 0 : distances.Values.Max();
    }

    /// <summary>
    /// The s-diameter: the greatest s-distance over every pair of nodes that an s-walk connects —
    /// null when no two distinct nodes are s-connected (an empty domain, or one where every node
    /// is s-isolated), per spec §5.3's "null if the graph has no s-connected pair."
    ///
    /// Same deliberate divergence from HNX as <see cref="GetSEccentricity"/>: HNX's
    /// <c>diameter</c>/<c>edge_diameter</c> raise unless the whole hypergraph is s-connected, while
    /// this maximizes over the pairs that *are* connected, which is what spec §5.3 asks for and is
    /// defined on every input. On an s-connected hypergraph the two definitions coincide, and the
    /// oracle project pins that agreement.
    /// </summary>
    int? GetSDiameter(int s = 1, SDomain domain = SDomain.Vertices)
    {
        var nodes = SAdjacency.Nodes(this, domain);
        var adjacency = SAdjacency.Build(this, s, domain);

        int? diameter = null;
        foreach (var node in nodes)
        {
            foreach (var distance in SAdjacency.Distances(node, adjacency).Values)
            {
                if (diameter is null || distance > diameter) diameter = distance;
            }
        }
        return diameter;
    }
}
