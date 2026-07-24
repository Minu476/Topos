namespace Topos.Hypergraph;

/// <summary>
/// Stable identity for a vertex (spec §3, primitive 1).
///
/// <see cref="Index"/> is a monotonic, never-reused counter value (yamafaktory pattern) — a
/// Handle's logical identity never changes and is never recycled, even after the vertex it names
/// goes dormant (Invariant 1).
///
/// <see cref="Generation"/> is reserved for M4 physical-slot-relocation detection (EnTT pattern;
/// spec §3.5 / Q7, lean (a)). In M0 (in-memory, no compaction) it is always 0 and load-bearing
/// for nothing — the field exists now so the struct layout is stable through M4's compaction
/// addition, rather than forcing a breaking format change later.
/// </summary>
public readonly record struct Handle(uint Index, uint Generation = 0)
{
    /// <summary>
    /// A reserved sentinel value. <c>HypergraphKernel</c>'s own <c>TryGet</c>-style failures still
    /// return C#'s <c>default(Handle)</c> (Index 0) rather than this — that's indistinguishable
    /// from a real Handle #0 without checking the accompanying <c>bool</c>, and using
    /// <see cref="Invalid"/> there would be a real (if small) behavioral change, not a doc fix, so
    /// it's left as a known gap rather than silently wired in. <c>IHypergraphQuery.HasCycle</c>'s
    /// internal DFS does use it, though — as the "no parent" sentinel for the root of each
    /// component, where no real vertex is a valid answer.
    /// </summary>
    public static readonly Handle Invalid = new(uint.MaxValue, uint.MaxValue);

    public bool IsValid => this != Invalid;

    public override string ToString() => Generation == 0 ? $"#{Index}" : $"#{Index}g{Generation}";
}
