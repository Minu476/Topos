namespace Topos.Hypergraph;

/// <summary>
/// Stable identity for a vertex (spec §3, primitive 1).
///
/// <see cref="Index"/> is a monotonic, never-reused counter value (yamafaktory pattern) — a
/// Handle's logical identity never changes and is never recycled, even after the vertex it names
/// goes dormant (Invariant 1).
///
/// <see cref="Generation"/> is reserved for a future physical-slot-compaction milestone (EnTT
/// pattern; spec §3.5 / Q7, lean (a)) — <b>not M4</b>: M4 shipped tiered persistence (snapshot
/// save/reload) without needing it, since snapshot reload restores Handles verbatim rather than
/// relocating live slots. It is always 0 today and load-bearing for nothing — the field exists so
/// the struct layout is stable if a future compaction pass needs it, rather than forcing a
/// breaking format change later. Settled M8, see `docs/DECISIONS.md`.
/// </summary>
public readonly record struct Handle(uint Index, uint Generation = 0)
{
    /// <summary>
    /// The reserved sentinel for "no valid Handle." <b>Wired into failure paths as of M8</b> (see
    /// `docs/DECISIONS.md`): <see cref="HypergraphKernel.TryGetVertex"/> and its
    /// <see cref="IHypergraphQuery"/> peers (<c>FilteredView</c>, <c>UnionView</c>) set the out
    /// <c>Vertex</c>'s <see cref="Vertex.Handle"/> to this value on failure, rather than C#'s
    /// <c>default(Handle)</c> (Index 0) — which used to be indistinguishable from a real vertex
    /// #0 to any caller that skipped the accompanying <c>bool</c>. Always check the <c>bool</c>
    /// regardless; <see cref="Invalid"/> is a defense-in-depth signal, not the primary contract.
    /// Note this convention covers <c>Handle</c>-shaped results specifically — generic
    /// <c>TryGetProperty&lt;T&gt;</c> failures still return <c>default(T)</c>, since <c>T</c> need
    /// not be Handle-related at all. <c>IHypergraphQuery.HasCycle</c>'s internal DFS also uses
    /// this value as the "no parent" sentinel for the root of each component.
    /// </summary>
    public static readonly Handle Invalid = new(uint.MaxValue, uint.MaxValue);

    public bool IsValid => this != Invalid;

    public override string ToString() => Generation == 0 ? $"#{Index}" : $"#{Index}g{Generation}";
}
