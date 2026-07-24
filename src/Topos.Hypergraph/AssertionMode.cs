namespace Topos.Hypergraph;

/// <summary>
/// Epistemic status of a reified relationship (spec §7 pattern 13 — RDF 1.2's unasserted-triple-
/// term concept, extended with a third state for agent memory).
///
/// <b>Not a reserved Vertex field.</b> Unlike <see cref="VertexRoles"/>/<see cref="VertexStatus"/>,
/// nothing in core traversal reads this per-hop, so per spec §3.2 Q1's own rule ("reserved slot
/// only if read per-hop by core traversal") it belongs in a <see cref="PropertyKey{T}"/> pool
/// like any other typed attribute — no new kernel storage was needed to support this type,
/// which is itself a small validation that M0's <c>PropertyKey&lt;T&gt;</c> design was already
/// general enough for M2's requirement.
///
/// No storage-level convention is imposed beyond the type. Resolve a property key for it the
/// same way as any other typed attribute, e.g. <c>kernel.ResolveProperty&lt;AssertionMode&gt;("mode")</c>.
/// A vertex with no mode property set means "no mode recorded" — the kernel does not default a
/// missing value to <see cref="Asserted"/> or anything else; that interpretation, like all
/// domain semantics, is a layer-1 concern (spec §4.1: "the kernel does not judge").
/// </summary>
public enum AssertionMode : byte
{
    /// <summary>Committed as true.</summary>
    Asserted = 0,

    /// <summary>Referenced/quoted without commitment to truth — RDF 1.2's unasserted triple term (e.g. "X said Y," without asserting Y).</summary>
    Quoted = 1,

    /// <summary>A candidate belief, not yet confirmed or rejected — the agent-memory extension beyond RDF 1.2's asserted/quoted binary.</summary>
    Hypothesized = 2,
}
