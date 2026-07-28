using System.Text.Json;
using Topos.Hypergraph.Mcp;

namespace Topos.Hypergraph.Mcp.Tests;

/// <summary>
/// Round-trip tests for M10's tool surface (docs/MCP_SERVER_SPEC.md §6 item 6), calling
/// <see cref="ToposMcpServer"/>'s static tool methods directly rather than through a live MCP
/// transport — this exercises the same logic every method wraps without needing to stand up
/// stdio/InMemoryTransport plumbing in a unit test. The actual v1 exit criterion (an MCP-aware
/// agent creating a hyperedge and querying it back through the running server, zero C# written by
/// the agent's author) is a separate dogfooding step, not this test file's job.
///
/// All tests share one process-wide <c>HypergraphKernel</c> (the stateful-single-session design,
/// spec §5 fork (a)) via <see cref="ToposMcpServer"/>'s static field — each test creates its own
/// fresh vertices rather than asserting on absolute counts, so tests stay order-independent.
/// </summary>
public class ToposMcpServerTests
{
    [Fact]
    public void CreateVertex_RoundTrips_Through_GetVertex()
    {
        var created = Deserialize<VertexInfo>(ToposMcpServer.CreateVertex());
        var fetched = Deserialize<VertexInfo>(ToposMcpServer.GetVertex(created.Handle));

        Assert.Equal(created.Handle, fetched.Handle);
        Assert.False(fetched.IsEdge);
        Assert.False(fetched.IsDormant);
    }

    [Fact]
    public void CreateVertex_IsEdge_Sets_The_Edge_Role()
    {
        var edge = Deserialize<VertexInfo>(ToposMcpServer.CreateVertex(isEdge: true));
        Assert.True(edge.IsEdge);
    }

    [Fact]
    public void GetVertex_Returns_Null_For_Unallocated_Handle()
    {
        var result = ToposMcpServer.GetVertex("#999999");
        Assert.Equal("null", result);
    }

    [Fact]
    public void SetVertexStatus_Dormant_Then_Reactivate_RoundTrips()
    {
        var handle = Deserialize<VertexInfo>(ToposMcpServer.CreateVertex()).Handle;

        var dormant = Deserialize<VertexInfo>(ToposMcpServer.SetVertexStatus(handle, dormant: true));
        Assert.True(dormant.IsDormant);

        var active = Deserialize<VertexInfo>(ToposMcpServer.SetVertexStatus(handle, dormant: false));
        Assert.False(active.IsDormant);

        // Invariant 1: dormant vertices stay resolvable, never garbage-collected.
        var stillResolvable = ToposMcpServer.GetVertex(handle);
        Assert.NotEqual("null", stillResolvable);
    }

    [Fact]
    public void EndToEnd_Hyperedge_Bfs_And_RoleAware_Traversal()
    {
        // Mirrors the README's TripRole worked example: one hyperedge, one Speaker mentioning
        // three Mentions in the same turn — the "n-ary fact, not three binary edges" case.
        const byte speaker = 0;
        const byte mention = 1;

        var alice = Deserialize<VertexInfo>(ToposMcpServer.CreateVertex()).Handle;
        var kyoto = Deserialize<VertexInfo>(ToposMcpServer.CreateVertex()).Handle;
        var nara = Deserialize<VertexInfo>(ToposMcpServer.CreateVertex()).Handle;
        var osaka = Deserialize<VertexInfo>(ToposMcpServer.CreateVertex()).Handle;
        var mentionEdge = Deserialize<VertexInfo>(ToposMcpServer.CreateVertex(isEdge: true)).Handle;

        ToposMcpServer.AddIncidence(mentionEdge, alice, speaker, 0);
        ToposMcpServer.AddIncidence(mentionEdge, kyoto, mention, 1);
        ToposMcpServer.AddIncidence(mentionEdge, nara, mention, 2);
        ToposMcpServer.AddIncidence(mentionEdge, osaka, mention, 3);

        Assert.True(ToposMcpServer.IsReachable(alice, osaka));

        var bfsFromAlice = Deserialize<string[]>(ToposMcpServer.Bfs(alice));
        Assert.Contains(osaka, bfsFromAlice);

        var mentioned = Deserialize<string[]>(ToposMcpServer.DirectedBfs(alice, speaker, mention));
        Assert.Equal(new[] { alice, kyoto, nara, osaka }, mentioned.OrderBy(h => h));

        var roleFiltered = Deserialize<string[]>(ToposMcpServer.RoleFilteredMembers(alice, mention));
        Assert.Equal(new[] { kyoto, nara, osaka }, roleFiltered.OrderBy(h => h));

        var directedPath = Deserialize<string[]>(ToposMcpServer.DirectedShortestPath(alice, kyoto, speaker, mention));
        Assert.Equal(new[] { alice, kyoto }, directedPath);

        var hyperedgeVertices = Deserialize<IncidenceInfo[]>(ToposMcpServer.GetHyperedgeVertices(mentionEdge));
        Assert.Equal(4, hyperedgeVertices.Length);

        var aliceHyperedges = Deserialize<string[]>(ToposMcpServer.GetVertexHyperedges(alice));
        Assert.Contains(mentionEdge, aliceHyperedges);
    }

    [Fact]
    public void StringProperty_Set_Get_Remove_RoundTrips()
    {
        var handle = Deserialize<VertexInfo>(ToposMcpServer.CreateVertex()).Handle;
        const string name = "displayName";

        ToposMcpServer.SetProperty(handle, name, new PropertyValue { Type = "string", StringValue = "Alice" });
        var fetched = Deserialize<PropertyValue>(ToposMcpServer.GetProperty(handle, name, "string"));
        Assert.Equal("Alice", fetched.StringValue);

        Assert.True(ToposMcpServer.RemoveProperty(handle, name, "string"));
        Assert.Equal("null", ToposMcpServer.GetProperty(handle, name, "string"));
    }

    [Fact]
    public void NumberProperty_Set_Get_Remove_RoundTrips()
    {
        var handle = Deserialize<VertexInfo>(ToposMcpServer.CreateVertex()).Handle;
        const string name = "confidence";

        ToposMcpServer.SetProperty(handle, name, new PropertyValue { Type = "number", NumberValue = 0.75 });
        var fetched = Deserialize<PropertyValue>(ToposMcpServer.GetProperty(handle, name, "number"));
        Assert.Equal(0.75, fetched.NumberValue);

        Assert.True(ToposMcpServer.RemoveProperty(handle, name, "number"));
    }

    [Fact]
    public void BoolProperty_Set_Get_Remove_RoundTrips()
    {
        var handle = Deserialize<VertexInfo>(ToposMcpServer.CreateVertex()).Handle;
        const string name = "verified";

        ToposMcpServer.SetProperty(handle, name, new PropertyValue { Type = "bool", BoolValue = true });
        var fetched = Deserialize<PropertyValue>(ToposMcpServer.GetProperty(handle, name, "bool"));
        Assert.True(fetched.BoolValue);

        Assert.True(ToposMcpServer.RemoveProperty(handle, name, "bool"));
    }

    [Fact]
    public void SetProperty_Throws_When_TypeTag_Disagrees_With_Populated_Field()
    {
        var handle = Deserialize<VertexInfo>(ToposMcpServer.CreateVertex()).Handle;

        Assert.Throws<ArgumentException>(() =>
            ToposMcpServer.SetProperty(handle, "bad", new PropertyValue { Type = "string", StringValue = null }));
    }

    [Fact]
    public void SetProperty_Throws_On_Unknown_Type()
    {
        var handle = Deserialize<VertexInfo>(ToposMcpServer.CreateVertex()).Handle;

        Assert.Throws<ArgumentException>(() =>
            ToposMcpServer.SetProperty(handle, "bad", new PropertyValue { Type = "date", StringValue = "2026-07-27" }));
    }

    [Fact]
    public void ParseHandle_Throws_On_Malformed_Wire_String()
    {
        Assert.Throws<ArgumentException>(() => ToposMcpServer.GetVertex("not-a-handle"));
    }

    [Fact]
    public void SemanticRecall_Finds_Nearest_Embedding()
    {
        var near = Deserialize<VertexInfo>(ToposMcpServer.CreateVertex()).Handle;
        var far = Deserialize<VertexInfo>(ToposMcpServer.CreateVertex()).Handle;
        const string property = "vec";

        ToposMcpServer.SetProperty(near, property, new PropertyValue { Type = "embedding", EmbeddingValue = [1f, 0f, 0f] });
        ToposMcpServer.SetProperty(far, property, new PropertyValue { Type = "embedding", EmbeddingValue = [0f, 0f, 100f] });

        var neighbors = Deserialize<NeighborResult[]>(ToposMcpServer.SemanticRecall(property, [1f, 0f, 0f], 1));

        Assert.Single(neighbors);
        Assert.Equal(near, neighbors[0].Handle);
    }

    [Fact]
    public void ConnectedComponents_Includes_A_Freshly_Created_Isolated_Vertex()
    {
        var isolated = Deserialize<VertexInfo>(ToposMcpServer.CreateVertex()).Handle;

        var components = Deserialize<string[][]>(ToposMcpServer.ConnectedComponents());

        Assert.Contains(components, c => c.Contains(isolated));
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json) ?? throw new InvalidOperationException($"Unexpected null deserializing {typeof(T)} from: {json}");
}
