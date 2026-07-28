using System.ComponentModel;
using ModelContextProtocol.Server;
using Topos.Hypergraph.Knowledge;

namespace Topos.Hypergraph.Mcp;

/// <summary>
/// M10 (docs/MCP_SERVER_SPEC.md, approved 2026-07-27): exposes Topos's public API as MCP tools.
/// Stateful, single-session per Nasser's approved §5 fork (a) lean — one <see cref="Kernel"/>
/// lives for the server process's lifetime; the agent builds and queries it across turns; state is
/// lost on process exit (persistence is a v2 concern, gated on a real consumer needing it).
///
/// This class is deliberately a thin wrapper: every method is a direct call into
/// <see cref="HypergraphKernel"/>, <see cref="IHypergraphQuery"/>, <see cref="DirectedTraversal"/>,
/// or <see cref="VectorIndex"/>. It stays dumb and faithful, the same "kernel records; it does not
/// judge" philosophy the spec's §2.3 maps onto MCP — the agent (layer 1) decides what role bytes
/// mean and what cardinalities to enforce, not this server.
/// </summary>
[McpServerToolType]
public sealed class ToposMcpServer
{
    private static readonly HypergraphKernel Kernel = new();

    // ── Vertices ─────────────────────────────────────────────────────────────

    [McpServerTool(Name = "create_vertex"), Description("Creates a new vertex with a fresh, never-reused handle. Set isEdge=true to reify it as a hyperedge (the Role:Edge pattern) rather than a domain vertex.")]
    public static string CreateVertex(bool isEdge = false)
    {
        var handle = Kernel.CreateVertex(isEdge ? VertexRoles.Edge : VertexRoles.None);
        return TypeMapping.ToJson(ToVertexInfo(handle));
    }

    [McpServerTool(Name = "get_vertex"), Description("Looks up a vertex by handle, including dormant ones. Returns null if the handle was never allocated.")]
    public static string GetVertex([Description("An opaque handle string, e.g. \"#3\".")] string handle)
    {
        var h = TypeMapping.ParseHandle(handle);
        return TypeMapping.ToJson(Kernel.TryGetVertex(h, out var v)
            ? new VertexInfo(handle, v.Roles.HasFlag(VertexRoles.Edge), v.IsDormant)
            : null);
    }

    [McpServerTool(Name = "count_vertices"), Description("Total vertex count, including dormant ones.")]
    public static int CountVertices() => Kernel.CountVertices();

    [McpServerTool(Name = "set_vertex_status"), Description("Sets a vertex dormant (tombstoned but still resolvable, never garbage-collected) or reactivates it.")]
    public static string SetVertexStatus(string handle, bool dormant)
    {
        var h = TypeMapping.ParseHandle(handle);
        if (dormant) Kernel.SetDormant(h); else Kernel.Reactivate(h);
        return TypeMapping.ToJson(ToVertexInfo(h));
    }

    // ── Incidences ───────────────────────────────────────────────────────────

    [McpServerTool(Name = "add_incidence"), Description("Records one membership: member participates in source (a hyperedge vertex) under role at position ordinal. No existence check beyond having been allocated at some point.")]
    public static string AddIncidence(string source, string member, byte role, int ordinal)
    {
        var incidence = Kernel.AddIncidence(TypeMapping.ParseHandle(source), TypeMapping.ParseHandle(member), role, ordinal);
        return TypeMapping.ToJson(ToIncidenceInfo(incidence));
    }

    [McpServerTool(Name = "get_hyperedge_vertices"), Description("Every incidence (participant + role + ordinal) recorded on a hyperedge vertex.")]
    public static string GetHyperedgeVertices(string hyperedge)
    {
        var members = Kernel.GetHyperedgeVertices(TypeMapping.ParseHandle(hyperedge));
        return TypeMapping.ToJson(members.Select(ToIncidenceInfo).ToArray());
    }

    [McpServerTool(Name = "get_vertex_hyperedges"), Description("Every hyperedge a vertex is a member of, as handle strings.")]
    public static string GetVertexHyperedges(string vertex)
    {
        var edges = Kernel.GetVertexHyperedges(TypeMapping.ParseHandle(vertex));
        return TypeMapping.ToJson(edges.Select(h => h.ToString()).ToArray());
    }

    // ── Properties (tagged union — spec §5 fork (e)) ────────────────────────

    [McpServerTool(Name = "set_property"), Description("Sets a typed property value on a vertex, creating the property pool on first use. value.Type selects which of value's other fields is read: \"string\", \"number\", \"bool\", or \"embedding\".")]
    public static string SetProperty(string handle, string name, PropertyValue value)
    {
        var h = TypeMapping.ParseHandle(handle);
        switch (value.Type)
        {
            case "string":
                Kernel.SetProperty(Kernel.ResolveProperty<string>(name), h,
                    value.StringValue ?? throw new ArgumentException("StringValue is required when Type is \"string\".", nameof(value)));
                break;
            case "number":
                Kernel.SetProperty(Kernel.ResolveProperty<double>(name), h,
                    value.NumberValue ?? throw new ArgumentException("NumberValue is required when Type is \"number\".", nameof(value)));
                break;
            case "bool":
                Kernel.SetProperty(Kernel.ResolveProperty<bool>(name), h,
                    value.BoolValue ?? throw new ArgumentException("BoolValue is required when Type is \"bool\".", nameof(value)));
                break;
            case "embedding":
                Kernel.SetProperty(Kernel.ResolveProperty<float[]>(name), h,
                    value.EmbeddingValue ?? throw new ArgumentException("EmbeddingValue is required when Type is \"embedding\".", nameof(value)));
                break;
            default:
                throw new ArgumentException($"Unknown property Type \"{value.Type}\" — expected \"string\", \"number\", \"bool\", or \"embedding\".", nameof(value));
        }
        return "ok";
    }

    [McpServerTool(Name = "get_property"), Description("Gets a typed property value from a vertex. type selects which pool to read: \"string\", \"number\", \"bool\", or \"embedding\". Returns null if no value was ever set.")]
    public static string GetProperty(string handle, string name, string type)
    {
        var h = TypeMapping.ParseHandle(handle);
        PropertyValue? result = type switch
        {
            "string" => Kernel.TryGetProperty(Kernel.ResolveProperty<string>(name), h, out var s) ? new PropertyValue { Type = "string", StringValue = s } : null,
            "number" => Kernel.TryGetProperty(Kernel.ResolveProperty<double>(name), h, out var n) ? new PropertyValue { Type = "number", NumberValue = n } : null,
            "bool" => Kernel.TryGetProperty(Kernel.ResolveProperty<bool>(name), h, out var b) ? new PropertyValue { Type = "bool", BoolValue = b } : null,
            "embedding" => Kernel.TryGetProperty(Kernel.ResolveProperty<float[]>(name), h, out var e) ? new PropertyValue { Type = "embedding", EmbeddingValue = e } : null,
            _ => throw new ArgumentException($"Unknown property type \"{type}\" — expected \"string\", \"number\", \"bool\", or \"embedding\".", nameof(type)),
        };
        return TypeMapping.ToJson(result);
    }

    [McpServerTool(Name = "remove_property"), Description("Removes a vertex's value for a typed property, if any. type selects which pool: \"string\", \"number\", \"bool\", or \"embedding\". Returns whether a value was actually present to remove.")]
    public static bool RemoveProperty(string handle, string name, string type)
    {
        var h = TypeMapping.ParseHandle(handle);
        return type switch
        {
            "string" => Kernel.RemoveProperty(Kernel.ResolveProperty<string>(name), h),
            "number" => Kernel.RemoveProperty(Kernel.ResolveProperty<double>(name), h),
            "bool" => Kernel.RemoveProperty(Kernel.ResolveProperty<bool>(name), h),
            "embedding" => Kernel.RemoveProperty(Kernel.ResolveProperty<float[]>(name), h),
            _ => throw new ArgumentException($"Unknown property type \"{type}\" — expected \"string\", \"number\", \"bool\", or \"embedding\".", nameof(type)),
        };
    }

    // ── Kernel-level query (role-blind, topology-only adjacency) ────────────

    [McpServerTool(Name = "is_reachable"), Description("Whether \"to\" is reachable from \"from\" via topology-only (role-blind) adjacency — any two vertices co-incident on the same hyperedge count as adjacent.")]
    public static bool IsReachable(string from, string to) =>
        ((IHypergraphQuery)Kernel).IsReachable(TypeMapping.ParseHandle(from), TypeMapping.ParseHandle(to));

    [McpServerTool(Name = "shortest_path"), Description("One shortest topology-only path from \"from\" to \"to\", as an ordered handle-string list including both endpoints. Empty if unreachable.")]
    public static string ShortestPath(string from, string to)
    {
        var path = ((IHypergraphQuery)Kernel).GetShortestPath(TypeMapping.ParseHandle(from), TypeMapping.ParseHandle(to));
        return TypeMapping.ToJson(path.Select(h => h.ToString()).ToArray());
    }

    [McpServerTool(Name = "bfs"), Description("Breadth-first traversal from start over topology-only (role-blind) adjacency, as a handle-string list in visit order.")]
    public static string Bfs(string start)
    {
        var visited = ((IHypergraphQuery)Kernel).GetBfs(TypeMapping.ParseHandle(start));
        return TypeMapping.ToJson(visited.Select(h => h.ToString()).ToArray());
    }

    [McpServerTool(Name = "connected_components"), Description("Every connected component (topology-only adjacency) across the whole graph, domain and hyperedge vertices alike, as a list of handle-string lists.")]
    public static string ConnectedComponents()
    {
        var components = ((IHypergraphQuery)Kernel).GetConnectedComponents();
        return TypeMapping.ToJson(components.Select(c => c.Select(h => h.ToString()).ToArray()).ToArray());
    }

    // ── Knowledge (M9) — role-aware directed traversal ──────────────────────

    [McpServerTool(Name = "directed_bfs"), Description("Role-aware directed BFS (Topos.Hypergraph.Knowledge, M9): follows only hyperedges where the current frontier vertex holds fromRole, landing on that edge's toRole members. Returns visited handle-strings in discovery order.")]
    public static string DirectedBfs(string start, byte fromRole, byte toRole)
    {
        var visited = ((IHypergraphQuery)Kernel).DirectedBfs(TypeMapping.ParseHandle(start), fromRole, toRole);
        return TypeMapping.ToJson(visited.Select(h => h.ToString()).ToArray());
    }

    [McpServerTool(Name = "directed_shortest_path"), Description("One shortest directed path from \"from\" to \"to\" (Topos.Hypergraph.Knowledge, M9), following only fromRole→toRole hyperedge legs. Empty if unreachable.")]
    public static string DirectedShortestPath(string from, string to, byte fromRole, byte toRole)
    {
        var path = ((IHypergraphQuery)Kernel).DirectedShortestPath(TypeMapping.ParseHandle(from), TypeMapping.ParseHandle(to), fromRole, toRole);
        return TypeMapping.ToJson(path.Select(h => h.ToString()).ToArray());
    }

    [McpServerTool(Name = "role_filtered_members"), Description("The members of vertex's hyperedges that hold role (Topos.Hypergraph.Knowledge, M9) — the one-hop role-filtered case.")]
    public static string RoleFilteredMembers(string vertex, byte role)
    {
        var members = ((IHypergraphQuery)Kernel).RoleFilteredMembers(TypeMapping.ParseHandle(vertex), role);
        return TypeMapping.ToJson(members.Select(h => h.ToString()).ToArray());
    }

    // ── Semantic recall (M5 embeddings) ─────────────────────────────────────

    [McpServerTool(Name = "semantic_recall"), Description("Exact k-nearest-neighbor search (brute-force, not approximate) over a float[] embedding property named propertyName — requires that property to have been set via set_property with type \"embedding\" on at least one vertex.")]
    public static string SemanticRecall(string propertyName, float[] query, int k)
    {
        var index = new VectorIndex(Kernel, Kernel.ResolveProperty<float[]>(propertyName));
        var neighbors = index.NearestNeighbors(query, k);
        return TypeMapping.ToJson(neighbors.Select(n => new NeighborResult(n.Handle.ToString(), n.Distance)).ToArray());
    }

    private static VertexInfo ToVertexInfo(Handle handle)
    {
        Kernel.TryGetVertex(handle, out var v);
        return new VertexInfo(handle.ToString(), v.Roles.HasFlag(VertexRoles.Edge), v.IsDormant);
    }

    private static IncidenceInfo ToIncidenceInfo(Incidence incidence) =>
        new(incidence.Source.ToString(), incidence.Member.ToString(), incidence.Role, incidence.Ordinal);
}
