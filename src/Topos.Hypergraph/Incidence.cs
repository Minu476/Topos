namespace Topos.Hypergraph;

/// <summary>
/// One membership (spec §3, primitive 3): <see cref="Member"/> participates in
/// <see cref="Source"/> under <see cref="Role"/> at position <see cref="Ordinal"/>.
///
/// <see cref="Role"/> is a raw byte — the kernel does not interpret it. Domain meaning (e.g.
/// RLB's Anchor=0/Condition=1/Target=2) lives in the layer-1 Knowledge model (spec §4.1), which
/// also owns cardinality validation (e.g. RLB's D2: exactly one Anchor, one Target). The kernel
/// records; it does not judge. See `docs/ROLE_CONVENTIONS.md` (settled M8) for the recommended
/// byte-backed-enum pattern for defining and casting role values — no kernel change, just a
/// documented convention so consumers stop reinventing <c>const byte</c> role fields.
///
/// For a reified hyperedge, <see cref="Source"/> is the Handle of the vertex tagged
/// <see cref="VertexRoles.Edge"/>; <see cref="Member"/> is a participant vertex.
///
/// <b>Cell-level properties (theta, confidence, per-subgoal success rate) are a layer-1
/// concern, not a kernel one — settled M8, see `docs/DECISIONS.md`.</b> The kernel's
/// <c>PropertyKey{T}</c> pools key on a single <see cref="Handle"/>
/// (<see cref="HypergraphKernel.SetProperty{T}"/>); there is no
/// <c>SetProperty(key, incidence, value)</c> overload keyed on the
/// (Source, Member, Ordinal) triple, and M8 decided not to add one — the two real consumers that
/// wanted per-cell data (ChatMemory, NexusVerifier) both got by fine without it. If you need data
/// that's genuinely per-membership rather than per-vertex, either (a) reify that membership as
/// its own edge-vertex (M2 nested reification supports this natively — each membership becomes a
/// first-class vertex you can attach ordinary per-Handle properties to), or (b) keep a side index
/// keyed on <c>(Source, Member, Ordinal)</c> in your own layer-1 code. Both are the pattern real
/// consumers already use; there's no kernel-level shortcut.
/// </summary>
public readonly record struct Incidence(Handle Source, Handle Member, byte Role, int Ordinal);
