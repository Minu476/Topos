namespace Topos.Hypergraph;

/// <summary>
/// Reserved hot-path field (spec §3.2 Q1: read per-hop to skip dormant vertices during
/// traversal). Dormant is a tombstone, not deletion — Invariant 1 (spec §3): dormant vertices are
/// never garbage-collected and remain resolvable, including as provenance targets.
/// </summary>
public enum VertexStatus : byte
{
    Active = 0,
    Dormant = 1,
}
