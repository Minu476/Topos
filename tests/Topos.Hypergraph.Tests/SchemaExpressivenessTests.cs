using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

/// <summary>
/// M3's exit criterion, taken literally (spec §6): "A real schema (nodes + relationships +
/// provenance) expressible without ad hoc tables." Everything else in M0-M3 built the
/// *mechanisms* (PropertyKey&lt;T&gt;, Incidence, reification, AssertionMode, views); this is the
/// one test that actually demonstrates the claim end-to-end on a realistic small schema, rather
/// than leaving it implied by the mechanisms existing separately.
///
/// A tiny "who works where" knowledge graph: Person and Company node types (via a typed "kind"
/// property, not a schema table), a WorksAt relationship (a reified edge, not a join table), and
/// provenance on that relationship (source + recorded-at properties + AssertionMode) -- all
/// expressed with primitives that already existed before this test, nothing new added to make
/// this example work.
/// </summary>
public class SchemaExpressivenessTests
{
    private enum NodeKind { Person, Company }

    [Fact]
    public void RealisticSchema_NodesRelationshipsAndProvenance_NoAdHocTables()
    {
        var kernel = new HypergraphKernel();
        var kind = kernel.ResolveProperty<NodeKind>("kind");
        var name = kernel.ResolveProperty<string>("name");
        var source = kernel.ResolveProperty<string>("source");
        var recordedAt = kernel.ResolveProperty<DateOnly>("recordedAt");
        var mode = kernel.ResolveProperty<AssertionMode>("mode");

        // ── Nodes: two types, distinguished by a typed property, not a schema table ──────────
        var alice = kernel.CreateVertex();
        kernel.SetProperty(kind, alice, NodeKind.Person);
        kernel.SetProperty(name, alice, "Alice");

        var acme = kernel.CreateVertex();
        kernel.SetProperty(kind, acme, NodeKind.Company);
        kernel.SetProperty(name, acme, "Acme Corp");

        // ── Relationship: a reified edge, not a join table ──────────────────────────────────
        var worksAt = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(worksAt, alice, role: 0 /* subject */, ordinal: 0);
        kernel.AddIncidence(worksAt, acme, role: 2 /* object */, ordinal: 1);

        // ── Provenance: properties on the relationship itself, not a side table ────────────
        kernel.SetProperty(source, worksAt, "LinkedIn import, 2026-07-24");
        kernel.SetProperty(recordedAt, worksAt, new DateOnly(2026, 7, 24));
        kernel.SetProperty(mode, worksAt, AssertionMode.Asserted);

        // ── Query the schema back, entirely through existing primitives ────────────────────
        Assert.True(kernel.TryGetProperty(kind, alice, out var aliceKind));
        Assert.Equal(NodeKind.Person, aliceKind);
        Assert.True(kernel.TryGetProperty(name, alice, out var aliceName));
        Assert.Equal("Alice", aliceName);

        Assert.True(kernel.TryGetProperty(kind, acme, out var acmeKind));
        Assert.Equal(NodeKind.Company, acmeKind);

        var relationship = kernel.IncidencesFrom(worksAt);
        Assert.Equal(2, relationship.Length);
        Assert.Contains(relationship, i => i.Member == alice && i.Role == 0);
        Assert.Contains(relationship, i => i.Member == acme && i.Role == 2);

        Assert.True(kernel.TryGetProperty(source, worksAt, out var recordedSource));
        Assert.Equal("LinkedIn import, 2026-07-24", recordedSource);
        Assert.True(kernel.TryGetProperty(recordedAt, worksAt, out var recordedDate));
        Assert.Equal(new DateOnly(2026, 7, 24), recordedDate);
        Assert.True(kernel.TryGetProperty(mode, worksAt, out var recordedMode));
        Assert.Equal(AssertionMode.Asserted, recordedMode);

        // ── A schema-level query: "who works at Acme?" via GetVertexHyperedges, not SQL ────
        IHypergraphQuery query = kernel;
        var aliceEdges = query.GetVertexHyperedges(alice);
        Assert.Contains(worksAt, aliceEdges);
    }

    [Fact]
    public void SameEdge_CanCarryUnrelatedProvenanceFacts_WithoutSchemaMigration()
    {
        // Adding a wholly new fact about an existing relationship (e.g. a confidence score
        // discovered later) needs no ALTER TABLE -- just resolve a new PropertyKey and set it.
        var kernel = new HypergraphKernel();
        var alice = kernel.CreateVertex();
        var acme = kernel.CreateVertex();
        var worksAt = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(worksAt, alice, 0, 0);
        kernel.AddIncidence(worksAt, acme, 2, 1);

        var source = kernel.ResolveProperty<string>("source");
        kernel.SetProperty(source, worksAt, "LinkedIn import");

        // Later, a second, unrelated fact is added about the same edge -- no schema change.
        var confidence = kernel.ResolveProperty<double>("confidence");
        kernel.SetProperty(confidence, worksAt, 0.92);

        Assert.True(kernel.TryGetProperty(source, worksAt, out _));
        Assert.True(kernel.TryGetProperty(confidence, worksAt, out var c));
        Assert.Equal(0.92, c);
    }
}
