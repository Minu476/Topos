using System.Text.Json;
using System.Text.RegularExpressions;

namespace Topos.Hypergraph.Mcp;

/// <summary>
/// The wire-format shapes M10 (docs/MCP_SERVER_SPEC.md §5) resolves for crossing the JSON-RPC
/// boundary: <see cref="Handle"/> as the opaque string <see cref="Handle.ToString"/> already
/// produces (§5 fork (d)), and typed property values as an explicit tagged union (§5 fork (e)) —
/// both per Nasser's approved lean, not the untyped/JSON-object alternatives.
/// </summary>
public static class TypeMapping
{
    private static readonly Regex HandlePattern = new(@"^#(?<index>\d+)(?:g(?<generation>\d+))?$", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    /// <summary>
    /// Parses a <see cref="Handle"/> back from the wire string a tool call receives — the same
    /// format <see cref="Handle.ToString"/> produces ("#3" or "#3g1"), so a handle an agent
    /// received from one tool call round-trips unchanged into the next.
    /// </summary>
    public static Handle ParseHandle(string wire)
    {
        var match = HandlePattern.Match(wire);
        if (!match.Success)
        {
            throw new ArgumentException(
                $"\"{wire}\" is not a valid handle — expected the format Handle.ToString() produces, e.g. \"#3\" or \"#3g1\".",
                nameof(wire));
        }

        var index = uint.Parse(match.Groups["index"].Value);
        var generation = match.Groups["generation"].Success ? uint.Parse(match.Groups["generation"].Value) : 0u;
        return new Handle(index, generation);
    }

    /// <summary>
    /// JSON-serializes a tool result for return as a plain string — matching the SDK's own
    /// samples' convention for structured output (e.g. <c>PrintEnvTool</c> in the
    /// ModelContextProtocol C# SDK's EverythingServer sample) rather than assuming a record/list
    /// return type auto-serializes, which the samples don't demonstrate.
    /// </summary>
    public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
}

/// <summary>
/// A typed property value crossing the wire (spec §5 fork (e), tagged union). Exactly one of the
/// four <c>*Value</c> fields is meaningful, selected by <see cref="Type"/> — one of "string",
/// "number", "bool", or "embedding". Mirrors <c>PropertyKey&lt;T&gt;</c>'s own type-safety
/// philosophy rather than papering over it with untyped JSON.
/// </summary>
public sealed record PropertyValue
{
    public required string Type { get; init; }
    public string? StringValue { get; init; }
    public double? NumberValue { get; init; }
    public bool? BoolValue { get; init; }
    public float[]? EmbeddingValue { get; init; }
}

public sealed record VertexInfo(string Handle, bool IsEdge, bool IsDormant);

public sealed record IncidenceInfo(string Source, string Member, byte Role, int Ordinal);

public sealed record NeighborResult(string Handle, float Distance);
