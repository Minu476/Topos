namespace Topos.Hypergraph;

/// <summary>
/// Reserved hot-path field (spec §3.2 Q1: read per-hop by core traversal, so it sits inline on
/// <see cref="Vertex"/> rather than behind a PropertyBag indirection).
///
/// Kernel-level roles only. Domain-specific role semantics — e.g. RLB's Anchor/Condition/Target
/// — live in the layer-1 Knowledge model, not here (spec §4.1: "the kernel does not judge").
/// <see cref="Edge"/> is the one kernel-level role: it marks a vertex as a reified hyperedge (the
/// Role:Edge pattern, spec §7 pattern 12) rather than a domain vertex.
/// </summary>
[Flags]
public enum VertexRoles : byte
{
    None = 0,
    Edge = 1 << 0,
}
