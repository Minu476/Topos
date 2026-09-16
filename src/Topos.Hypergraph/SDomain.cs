namespace Topos.Hypergraph;

/// <summary>
/// Which of a hypergraph's two dual node-sets an s-connectivity query runs over (M11 phase 2,
/// `docs/ALGORITHM_SPEC.md` §5.1-5.3).
///
/// <b>Why this parameter exists at all.</b> "s-adjacency" is two different relations depending on
/// which side of the incidence structure you stand on, and the s-walk literature uses both:
/// <see cref="Hyperedges"/> is the one <see cref="SWalk"/> already implements (and the one the
/// HNX oracle defaults to), while <see cref="Vertices"/> is the one `ALGORITHM_SPEC.md` §5.1's own
/// signature and its `ALGORITHM_GAP_LIST.md` §4.2 reduction anchor ("s=1 must equal the
/// GDS-verified <see cref="IHypergraphQuery.GetConnectedComponents"/>") are written against. The
/// spec contradicted itself on which one it meant — §5.1's prose says vertices, its implementation
/// note says "reuse <see cref="SWalk"/>", which is edges. Rather than silently pick one and lose
/// half the verification surface, both ship: see `docs/DECISIONS.md` 2026-09-16.
///
/// Both dualities are real and each anchors a different oracle:
/// <see cref="Vertices"/> anchors to <see cref="IHypergraphQuery.GetConnectedComponents"/> (T5,
/// GDS-verified) and to HNX's <c>edges=False</c> mode; <see cref="Hyperedges"/> anchors to
/// <see cref="SWalk"/> (T5, in-repo) and to HNX's <c>edges=True</c> default plus xgi's
/// <c>to_line_graph</c> (T2, two independent lineages).
/// </summary>
public enum SDomain
{
    /// <summary>
    /// Nodes are the hypergraph's <b>member vertices</b>; two are s-adjacent when they are
    /// co-members of at least <c>s</c> common hyperedges. The node set is every handle that
    /// appears as an <see cref="Incidence.Member"/> of at least one hyperedge — which is exactly
    /// HNX's node set, and is why a handle belonging to no hyperedge (a freshly-created vertex, or
    /// a typical hyperedge that is not itself nested inside another) is absent rather than
    /// reported as a singleton: it has no s-adjacency at any <c>s</c>, so a singleton for it would
    /// carry no information and would break oracle parity.
    /// </summary>
    Vertices,

    /// <summary>
    /// Nodes are the hypergraph's <b>hyperedges</b> (<see cref="VertexRoles.Edge"/>-tagged
    /// vertices); two are s-adjacent when they share at least <c>s</c> common member vertices.
    /// This is <see cref="SWalk"/>'s relation, the s-line-graph's substrate, and HNX's
    /// <c>edges=True</c> default. Every hyperedge is a node even when it has fewer than <c>s</c>
    /// members and therefore cannot be s-adjacent to anything — matching HNX and xgi, which both
    /// keep such an edge in the line graph as an isolated node.
    /// </summary>
    Hyperedges
}
