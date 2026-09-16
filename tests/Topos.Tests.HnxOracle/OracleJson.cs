using System.Text.Json;

namespace Topos.Tests.HnxOracle;

/// <summary>Reads the worker's two response shapes back into the forms the assertions compare.</summary>
internal static class OracleJson
{
    public static List<List<string>> Components(JsonElement result) =>
        [.. result.GetProperty("components").EnumerateArray()
            .Select(c => c.EnumerateArray().Select(n => n.GetString()!).ToList())];

    public static Dictionary<string, List<string>> Adjacency(JsonElement result) =>
        result.GetProperty("adjacency").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(n => n.GetString()!).ToList());

    public static Dictionary<string, int> Eccentricity(JsonElement result) =>
        result.GetProperty("eccentricity").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetInt32());
}
