namespace Topos.Hypergraph;

/// <summary>
/// One membership (spec §3, primitive 3): <see cref="Member"/> participates in
/// <see cref="Source"/> under <see cref="Role"/> at position <see cref="Ordinal"/>.
///
/// <see cref="Role"/> is a raw byte — the kernel does not interpret it. Domain meaning (e.g.
/// RLB's Anchor=0/Condition=1/Target=2) lives in the layer-1 Knowledge model (spec §4.1), which
/// also owns cardinality validation (e.g. RLB's D2: exactly one Anchor, one Target). The kernel
/// records; it does not judge.
///
/// For a reified hyperedge, <see cref="Source"/> is the Handle of the vertex tagged
/// <see cref="VertexRoles.Edge"/>; <see cref="Member"/> is a participant vertex. Cell-level
/// properties (theta, confidence, transition counts) attach to the (Source, Member, Ordinal)
/// triple via <see cref="PropertyKey{T}"/> pools keyed on the member Handle within that edge's
/// scope — the mechanism for this lands with M2's reification work; M0 only stores the
/// membership shape itself.
/// </summary>
public readonly record struct Incidence(Handle Source, Handle Member, byte Role, int Ordinal);
