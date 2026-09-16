using Topos.Hypergraph;

namespace Topos.Tests.HnxOracle;

/// <summary>
/// Turns a Topos hypergraph into the edge→members dictionary HyperNetX and xgi both construct
/// from, and maps the oracle's answers back onto <see cref="Handle"/>s.
///
/// The counterpart of <c>Topos.Tests.GdsOracle</c>'s projection engines, and much thinner than
/// they are — HNX is a hypergraph library, so there is no projection step and nothing to correct
/// for. That is the whole reason spec §3 reaches for HNX: the comparison is direct, with no
/// binary-projection story in between that could absorb a disagreement.
/// </summary>
internal static class HnxExport
{
    /// <summary>
    /// The edge→members dictionary, keyed by handle-derived names (<c>e12</c>, <c>v3</c>) so the
    /// mapping back is unambiguous.
    ///
    /// Refuses two shapes rather than exporting something HNX would quietly reinterpret:
    /// an empty hyperedge (HNX drops it, so the node sets would differ for a reason invisible in
    /// the diff) and a nested hyperedge — a <see cref="VertexRoles.Edge"/> vertex that is itself a
    /// member of another hyperedge, which Topos's reification allows but which would collide with
    /// its own edge name in HNX's flat namespace. Neither is a Topos limitation; both are cases
    /// where the two models genuinely differ, so the parity fixtures stay clear of them and the
    /// in-process tests cover them instead.
    /// </summary>
    public static Dictionary<string, List<string>> ToEdgeDictionary(IHypergraphQuery graph)
    {
        var edges = new HashSet<Handle>(graph.HyperedgeHandles());
        var result = new Dictionary<string, List<string>>();

        foreach (var edge in graph.HyperedgeHandles())
        {
            var members = graph.GetHyperedgeVertices(edge).Select(i => i.Member).Distinct().ToList();
            if (members.Count == 0)
                throw new InvalidOperationException($"Hyperedge {edge} has no members; HNX cannot represent it.");

            foreach (var member in members)
            {
                if (edges.Contains(member))
                    throw new InvalidOperationException(
                        $"Hyperedge {member} is nested inside {edge}; HNX has one flat namespace for nodes and edges.");
            }

            result[Name(edge)] = [.. members.Select(Name)];
        }
        return result;
    }

    /// <summary>The name this export gives a handle. <c>#12</c> would not survive JSON round-tripping cleanly.</summary>
    public static string Name(Handle handle) => $"h{handle.Index}";

    /// <summary>Handle → name, for the node set of one <see cref="SDomain"/>.</summary>
    public static string Domain(SDomain domain) => domain == SDomain.Vertices ? "vertices" : "hyperedges";

    /// <summary>
    /// Topos components rendered the way the oracle renders its own: sorted names, sorted
    /// components. Both sides are canonicalized so the comparison is about the partition itself,
    /// not about either side's iteration order.
    /// </summary>
    public static List<List<string>> Canonical(IReadOnlyList<IReadOnlyList<Handle>> components) =>
        [.. components
            .Select(c => c.Select(Name).OrderBy(n => n, StringComparer.Ordinal).ToList())
            .OrderBy(c => string.Join(",", c), StringComparer.Ordinal)];

    /// <summary>The same canonicalization for an adjacency map.</summary>
    public static Dictionary<string, List<string>> Canonical(IReadOnlyDictionary<Handle, IReadOnlyList<Handle>> adjacency) =>
        adjacency.ToDictionary(
            p => Name(p.Key),
            p => p.Value.Select(Name).OrderBy(n => n, StringComparer.Ordinal).ToList());
}
