namespace Topos.Hypergraph;

/// <summary>
/// A vertex record (spec §3, primitive 2). <see cref="Roles"/> and <see cref="Status"/> are
/// reserved struct fields, not PropertyBag entries — both are read on every traversal hop, and a
/// PropertyBag indirection on that inner loop is the wrong tier (spec §3.2 Q1). Typed properties
/// attach separately via <see cref="PropertyKey{T}"/> pools, keeping this record small.
/// </summary>
public readonly record struct Vertex(Handle Handle, VertexRoles Roles, VertexStatus Status)
{
    public bool IsDormant => Status == VertexStatus.Dormant;
}
