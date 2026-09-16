namespace Topos.Hypergraph;

/// <summary>
/// What kind of write touched a handle (M13, `docs/LIVING_GRAPH_SPEC.md` §5.2). Flags, because the
/// change log coalesces per handle: a vertex created, then given a property, then made dormant all
/// before a reader looks is reported once, as one entry carrying all three kinds — the question a
/// consumer is actually asking is "what do I need to re-read," and that is answered by the set of
/// touched handles, not by a transcript of every write.
///
/// <b>There is no incidence-removal kind, and that is settled, not pending.</b> Invariant 3 — an
/// incidence is never removed (`docs/CONCEPTS.md`, decided 2026-09-16) — closes the structural side
/// of this vocabulary: memberships are only ever added.
/// </summary>
[Flags]
public enum ChangeKind
{
    None = 0,

    /// <summary>A new vertex was allocated (<see cref="HypergraphKernel.CreateVertex"/>).</summary>
    VertexCreated = 1 << 0,

    /// <summary><see cref="HypergraphKernel.SetDormant"/> or <see cref="HypergraphKernel.Reactivate"/> actually moved the status.</summary>
    StatusChanged = 1 << 1,

    /// <summary>
    /// An incidence was added. Recorded against <b>both</b> participants — the edge-vertex that
    /// gained a member and the member that gained a membership — because a consumer watching either
    /// side needs to know, and a consumer watching only the member would otherwise never learn it
    /// had joined a relation.
    /// </summary>
    IncidenceAdded = 1 << 2,

    /// <summary>A typed property was written (<see cref="HypergraphKernel.SetProperty{T}"/>).</summary>
    PropertySet = 1 << 3,

    /// <summary>A typed property that was present got removed (<see cref="HypergraphKernel.RemoveProperty{T}"/>).</summary>
    PropertyRemoved = 1 << 4,
}

/// <summary>
/// One coalesced entry in the change log: a handle, everything that has happened to it since the
/// reader last looked, and the revision of the most recent of those writes.
/// </summary>
/// <param name="Revision">The <see cref="HypergraphKernel.CurrentRevision"/> value of the most recent write to <paramref name="Handle"/>.</param>
/// <param name="Handle">The touched handle.</param>
/// <param name="Kind">Every kind of write that has touched it since it was last reported, OR-ed together.</param>
public readonly record struct GraphChange(long Revision, Handle Handle, ChangeKind Kind);
