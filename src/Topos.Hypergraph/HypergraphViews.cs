namespace Topos.Hypergraph;

/// <summary>
/// Composable views and set algebra over <see cref="IHypergraphQuery"/> (spec §6 M3). Two
/// building blocks — <see cref="FilteredView"/> and <see cref="UnionView"/> — cover the whole
/// spec-named surface (subgraph, mask, union, intersect, diff):
/// <list type="bullet">
/// <item><see cref="Subgraph"/> / <see cref="Mask"/> — the same <see cref="FilteredView"/>
/// mechanism under two names, since a fixed vertex-set and a live predicate are the same
/// operation.</item>
/// <item><see cref="Union"/> — <see cref="UnionView"/> directly.</item>
/// <item><see cref="Intersect"/> / <see cref="Difference"/> — both are just a
/// <see cref="FilteredView"/> over <paramref name="a"/>, predicated on membership (or
/// non-membership) in <paramref name="b"/>. No new mechanism needed.</item>
/// </list>
///
/// <b>No <c>Unmodifiable</c> view exists, deliberately.</b> <see cref="IHypergraphQuery"/> has no
/// write members at all — every mutation lives on <see cref="HypergraphKernel"/> directly, outside
/// the interface. Handing out a <see cref="HypergraphKernel"/> reference typed as
/// <see cref="IHypergraphQuery"/> already *is* an unmodifiable view, at compile time, for free.
/// Building a wrapper class for this would be redundant scaffolding around something the type
/// system already guarantees.
///
/// <b>Set algebra "doubling as version-diff" (spec §6 M3), precisely.</b> This only works
/// meaningfully when both sides share a common Handle-identity space (see
/// <see cref="UnionView"/>'s doc for why two independent kernels don't). The technique that stays
/// inside one kernel's identity space on purpose: because <see cref="Handle.Index"/> is
/// monotonic and never reused (spec §3, Invariant on <see cref="HandleAllocator"/>),
/// <c>Subgraph(kernel, h =&gt; h.Index &lt; snapshotThreshold)</c> is a genuine, meaningful
/// "state as of an earlier point in this kernel's history" — not a coincidence, an actual
/// temporal cut of the same identity space. <c>Difference(later, earlier)</c> over two such
/// threshold snapshots gives you exactly "what's been added since," with no persistence layer
/// required (that's M4's eventual job for cross-process/cross-session snapshots; this covers
/// within-one-kernel-lifetime version-diffing today). See <c>SetAlgebraTests</c> for a worked
/// example.
/// </summary>
public static class HypergraphViews
{
    public static IHypergraphQuery Subgraph(IHypergraphQuery source, Func<Handle, bool> predicate) =>
        new FilteredView(source, predicate);

    public static IHypergraphQuery Mask(IHypergraphQuery source, Func<Handle, bool> predicate) =>
        new FilteredView(source, predicate);

    public static IHypergraphQuery Union(IHypergraphQuery a, IHypergraphQuery b) => new UnionView(a, b);

    public static IHypergraphQuery Intersect(IHypergraphQuery a, IHypergraphQuery b) =>
        new FilteredView(a, b.ContainsVertex);

    /// <summary>Vertices in <paramref name="a"/> but not <paramref name="b"/> — "what's in A that isn't in B."</summary>
    public static IHypergraphQuery Difference(IHypergraphQuery a, IHypergraphQuery b) =>
        new FilteredView(a, h => !b.ContainsVertex(h));
}
