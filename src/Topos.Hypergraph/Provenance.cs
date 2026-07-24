namespace Topos.Hypergraph;

/// <summary>
/// Where a fact came from (spec §6 M5: "provenance... first-class"). A value type like every
/// other typed attribute — provenance is "first-class" here in the sense of being a named,
/// designed type with clear semantics rather than an untyped string bolted on; it is NOT a new
/// storage mechanism (<see cref="PropertyKey{T}"/> already does the job, same as
/// <see cref="AssertionMode"/> in M2).
///
/// For <b>structural</b> provenance — which other facts a fact was derived from, not just an
/// external label — nested reification (spec §6 M2) is the actual mechanism: link a derived edge
/// to its source edges via <see cref="Incidence"/>, exactly as
/// <c>ReificationTests.DepthN_ChainOfNestedEdges_EveryLevelRoundTrips</c> already demonstrates.
/// This record is for the leaf case: provenance that terminates outside the graph (a document, a
/// user, an external system), not another in-graph fact.
/// </summary>
public readonly record struct Provenance(string Source, DateTimeOffset RecordedAt);
