using Topos.Hypergraph;
using Topos.Hypergraph.Persistence;

namespace Topos.Hypergraph.Persistence.Tests;

public class HypergraphSnapshotTests
{
    [Fact]
    public void RoundTrip_EmptyKernel()
    {
        var kernel = new HypergraphKernel();
        using var stream = new MemoryStream();

        HypergraphSnapshot.Save(kernel, stream);
        stream.Position = 0;
        var reloaded = HypergraphSnapshot.Load(stream);

        Assert.Equal(0, reloaded.CountVertices());
        Assert.Equal(0u, reloaded.NextHandleIndex);
    }

    [Fact]
    public void RoundTrip_VerticesAndIncidences_StructureIntact()
    {
        var kernel = new HypergraphKernel();
        var anchor = kernel.CreateVertex();
        var condition = kernel.CreateVertex();
        var target = kernel.CreateVertex();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge, anchor, role: 0, ordinal: 0);
        kernel.AddIncidence(edge, condition, role: 1, ordinal: 1);
        kernel.AddIncidence(edge, target, role: 2, ordinal: 2);

        using var stream = new MemoryStream();
        HypergraphSnapshot.Save(kernel, stream);
        stream.Position = 0;
        var reloaded = HypergraphSnapshot.Load(stream);

        Assert.Equal(4, reloaded.CountVertices());
        Assert.True(reloaded.TryGetVertex(edge, out var reloadedEdge));
        Assert.Equal(VertexRoles.Edge, reloadedEdge.Roles);

        var members = reloaded.IncidencesFrom(edge);
        Assert.Equal(3, members.Length);
        Assert.Contains(members, i => i.Member == anchor && i.Role == 0 && i.Ordinal == 0);
        Assert.Contains(members, i => i.Member == condition && i.Role == 1 && i.Ordinal == 1);
        Assert.Contains(members, i => i.Member == target && i.Role == 2 && i.Ordinal == 2);
    }

    [Fact]
    public void RoundTrip_DormantStatus_Preserved()
    {
        var kernel = new HypergraphKernel();
        var h = kernel.CreateVertex();
        kernel.SetDormant(h);

        using var stream = new MemoryStream();
        HypergraphSnapshot.Save(kernel, stream);
        stream.Position = 0;
        var reloaded = HypergraphSnapshot.Load(stream);

        Assert.True(reloaded.TryGetVertex(h, out var v));
        Assert.True(v.IsDormant); // Invariant 1 survives a round-trip too
    }

    [Fact]
    public void RoundTrip_HandleAllocatorContinuity_NoCollisionAfterReload()
    {
        // The Invariant-1-critical case: a vertex created *after* reload must never collide with
        // one restored *from* the snapshot.
        var kernel = new HypergraphKernel();
        var h0 = kernel.CreateVertex();
        var h1 = kernel.CreateVertex();

        using var stream = new MemoryStream();
        HypergraphSnapshot.Save(kernel, stream);
        stream.Position = 0;
        var reloaded = HypergraphSnapshot.Load(stream);

        var freshlyCreated = reloaded.CreateVertex();

        Assert.NotEqual(h0, freshlyCreated);
        Assert.NotEqual(h1, freshlyCreated);
        Assert.Equal(2u, freshlyCreated.Index); // h0=0, h1=1, so the next one is genuinely 2
        Assert.Equal(3, reloaded.CountVertices());
    }

    [Fact]
    public void RoundTrip_NestedReification_SurvivesIntact()
    {
        // Ties M2 (nested reification) and M4 (persistence) together -- a good integration check
        // that these two milestones' primitives compose correctly.
        var kernel = new HypergraphKernel();
        var x = kernel.CreateVertex();
        var y = kernel.CreateVertex();
        var edgeA = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edgeA, x, 0, 0);
        kernel.AddIncidence(edgeA, y, 2, 1);
        var z = kernel.CreateVertex();
        var edgeB = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edgeB, edgeA, 0, 0); // edgeA nested as a member of edgeB
        kernel.AddIncidence(edgeB, z, 2, 1);

        using var stream = new MemoryStream();
        HypergraphSnapshot.Save(kernel, stream);
        stream.Position = 0;
        var reloaded = HypergraphSnapshot.Load(stream);

        Assert.Contains(reloaded.IncidencesFrom(edgeB), i => i.Member == edgeA);
        Assert.Contains(reloaded.IncidencesOf(edgeA), i => i.Source == edgeB);
        Assert.True(reloaded.TryGetVertex(edgeA, out var edgeAVertex));
        Assert.Equal(VertexRoles.Edge, edgeAVertex.Roles);
    }

    [Fact]
    public void RoundTrip_MultipleTypedPropertyColumns()
    {
        var kernel = new HypergraphKernel();
        var confidence = kernel.ResolveProperty<double>("confidence");
        var label = kernel.ResolveProperty<string>("label");
        var visits = kernel.ResolveProperty<int>("visits");
        var mode = kernel.ResolveProperty<AssertionMode>("mode");

        var edge = kernel.CreateVertex(VertexRoles.Edge);
        kernel.SetProperty(confidence, edge, 0.87);
        kernel.SetProperty(label, edge, "learned-transition");
        kernel.SetProperty(visits, edge, 42);
        kernel.SetProperty(mode, edge, AssertionMode.Hypothesized);

        var columns = new IPersistedPropertyColumn[]
        {
            PersistedProperty.Double(confidence),
            PersistedProperty.String(label),
            PersistedProperty.Int32(visits),
            PersistedProperty.Custom(mode,
                static (w, v) => w.Write((byte)v),
                static r => (AssertionMode)r.ReadByte()),
        };

        using var stream = new MemoryStream();
        HypergraphSnapshot.Save(kernel, stream, columns);
        stream.Position = 0;
        var reloaded = HypergraphSnapshot.Load(stream, columns);

        Assert.True(reloaded.TryGetProperty(confidence, edge, out var c));
        Assert.Equal(0.87, c);
        Assert.True(reloaded.TryGetProperty(label, edge, out var l));
        Assert.Equal("learned-transition", l);
        Assert.True(reloaded.TryGetProperty(visits, edge, out var v));
        Assert.Equal(42, v);
        Assert.True(reloaded.TryGetProperty(mode, edge, out var m));
        Assert.Equal(AssertionMode.Hypothesized, m);
    }

    [Fact]
    public void Load_BadMagicNumber_Throws()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            writer.Write(0xDEADBEEF);
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => HypergraphSnapshot.Load(stream));
    }

    [Fact]
    public void Load_MissingPropertyColumnDescriptor_ThrowsWithClearMessage()
    {
        var kernel = new HypergraphKernel();
        var h = kernel.CreateVertex();
        var confidence = kernel.ResolveProperty<double>("confidence");
        kernel.SetProperty(confidence, h, 0.5);

        using var stream = new MemoryStream();
        HypergraphSnapshot.Save(kernel, stream, [PersistedProperty.Double(confidence)]);
        stream.Position = 0;

        var ex = Assert.Throws<InvalidDataException>(() => HypergraphSnapshot.Load(stream)); // no columns passed
        Assert.Contains("confidence", ex.Message);
    }
}
