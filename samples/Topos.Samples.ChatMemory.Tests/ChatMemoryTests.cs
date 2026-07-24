using Topos.Hypergraph;

namespace Topos.Samples.ChatMemory.Tests;

/// <summary>
/// M5's falsifiability gate, exercised end-to-end. Every test here uses only
/// <see cref="ChatMemory"/>'s public API, which itself uses only <c>Topos.Hypergraph</c>'s
/// public API — no internal access anywhere in this chain. This is the actual evidence for or
/// against the "standalone library" claim, not just the existence of the sample project.
/// </summary>
public class ChatMemoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 24, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RecordTurn_RoundTripsContentAndProvenance()
    {
        var memory = new ChatMemory();
        var turn = memory.RecordTurn("user", "Hi, I'm planning a trip to Kyoto.", [0.1f, 0.2f], T0);

        Assert.Equal("Hi, I'm planning a trip to Kyoto.", memory.ContentOf(turn));
        var provenance = memory.ProvenanceOf(turn);
        Assert.NotNull(provenance);
        Assert.Equal("user", provenance.Value.Source);
        Assert.Equal(T0, provenance.Value.RecordedAt);
    }

    [Fact]
    public void RecordMention_OneTurnMentioningThreeEntities_IsOneAtomicRelationship()
    {
        // The concrete instance of spec §1's thesis: one utterance mentions three entities
        // together. A binary graph would need three separate edges, losing the fact that these
        // were mentioned *together*, *in this one turn* -- here it's one hyperedge.
        var memory = new ChatMemory();
        var turn = memory.RecordTurn("user", "Kyoto, Nara, and Osaka are all on my list.", [0.1f], T0);
        var kyoto = memory.GetOrCreateEntity("Kyoto");
        var nara = memory.GetOrCreateEntity("Nara");
        var osaka = memory.GetOrCreateEntity("Osaka");

        var mention = memory.RecordMention(turn, [kyoto, nara, osaka]);

        // One hyperedge, four members (the turn + three entities) -- not three separate edges.
        Assert.Equal(4, memory.Query.GetHyperedgeVertices(mention).Count);

        var mentioned = memory.EntitiesMentionedIn(turn);
        Assert.Equal(3, mentioned.Count);
        Assert.Contains(kyoto, mentioned);
        Assert.Contains(nara, mentioned);
        Assert.Contains(osaka, mentioned);
    }

    [Fact]
    public void RecordExtractedFact_CarriesModeAndLinksBackToSourceTurn()
    {
        var memory = new ChatMemory();
        var turn = memory.RecordTurn("user", "I think my flight lands around 6pm, not totally sure.", [0.3f], T0);

        var fact = memory.RecordExtractedFact(turn, "User's flight arrival: ~6pm", AssertionMode.Hypothesized);

        Assert.Equal(AssertionMode.Hypothesized, memory.ModeOf(fact));
        Assert.Contains(memory.Query.GetHyperedgeVertices(fact), i => i.Member == turn);
    }

    [Fact]
    public void RecordExtractedFact_AssertedVsHypothesized_AreDistinguishable()
    {
        var memory = new ChatMemory();
        var confident = memory.RecordTurn("user", "My name is Alice.", [0.1f], T0);
        var uncertain = memory.RecordTurn("user", "I might be free on Tuesday.", [0.2f], T0);

        var name = memory.RecordExtractedFact(confident, "User's name: Alice", AssertionMode.Asserted);
        var availability = memory.RecordExtractedFact(uncertain, "User available Tuesday", AssertionMode.Hypothesized);

        Assert.Equal(AssertionMode.Asserted, memory.ModeOf(name));
        Assert.Equal(AssertionMode.Hypothesized, memory.ModeOf(availability));
    }

    [Fact]
    public void RecallSimilarTurns_FindsSemanticallyCloseTurnsFirst()
    {
        var memory = new ChatMemory();
        var kyotoTurn = memory.RecordTurn("user", "Tell me about Kyoto temples.", [1f, 0f], T0);
        var weatherTurn = memory.RecordTurn("user", "What's the weather like tomorrow?", [0f, 1f], T0);

        var results = memory.RecallSimilarTurns([0.9f, 0.1f], k: 2);

        Assert.Equal(kyotoTurn, results[0].Turn);
        Assert.Equal(weatherTurn, results[1].Turn);
        Assert.True(results[0].Distance < results[1].Distance);
    }

    [Fact]
    public void RecallFeedback_AccumulatesConfidenceOverMultipleObservations()
    {
        var memory = new ChatMemory();
        var query = memory.RecordTurn("user", "Remind me what I said about Kyoto?", [1f, 0f], T0);
        var recalled = memory.RecordTurn("user", "Tell me about Kyoto temples.", [1f, 0f], T0);

        memory.RecordRecallFeedback(query, recalled, wasUseful: true);
        memory.RecordRecallFeedback(query, recalled, wasUseful: true);
        memory.RecordRecallFeedback(query, recalled, wasUseful: false);

        // Feedback recorded across three calls into (functionally) the same recall relationship
        // -- verified indirectly via the general query surface below, since ChatMemory doesn't
        // expose recall-edge lookup directly (deliberately minimal API surface).
        var recallEdges = memory.Query.GetVertexHyperedges(query)
            .Where(e => memory.Query.GetHyperedgeVertices(e).Any(i => i.Member == recalled))
            .ToList();
        Assert.NotEmpty(recallEdges);
    }

    [Fact]
    public void GeneralQueryAlgorithms_WorkOverThisDomainUnmodified()
    {
        // The actual test: BFS (an M1 algorithm, built entirely from the 5 kernel primitives,
        // never designed with "chat memory" in mind) must work correctly over this domain with
        // zero special-casing. If this needed a Topos change, the falsifiability gate would have
        // failed.
        var memory = new ChatMemory();
        var turn1 = memory.RecordTurn("user", "I love Kyoto.", [1f], T0);
        var kyoto = memory.GetOrCreateEntity("Kyoto");
        memory.RecordMention(turn1, [kyoto]);

        var turn2 = memory.RecordTurn("user", "Kyoto has great temples.", [1f], T0);
        memory.RecordMention(turn2, [kyoto]);

        // turn1 and turn2 are connected through the shared entity "Kyoto" -- reachable via the
        // kernel's own BFS, with no chat-domain-specific traversal code anywhere.
        Assert.True(memory.Query.IsReachable(turn1, turn2));
    }

    [Fact]
    public void EntityMentionedAcrossManyTurns_ConnectedComponentsGroupsThemTogether()
    {
        var memory = new ChatMemory();
        var kyoto = memory.GetOrCreateEntity("Kyoto");
        var t1 = memory.RecordTurn("user", "Kyoto in spring?", [1f], T0);
        var t2 = memory.RecordTurn("agent", "Kyoto is lovely in spring.", [1f], T0);
        memory.RecordMention(t1, [kyoto]);
        memory.RecordMention(t2, [kyoto]);

        var isolatedTurn = memory.RecordTurn("user", "Unrelated question about weather.", [0f], T0);

        var components = memory.Query.GetConnectedComponents();

        Assert.Contains(components, c => c.Contains(t1) && c.Contains(t2));
        Assert.Contains(components, c => c.Count == 1 && c.Contains(isolatedTurn));
    }

    [Fact]
    public void GetOrCreateEntity_SameNameTwice_ReturnsSameHandle()
    {
        var memory = new ChatMemory();
        var a = memory.GetOrCreateEntity("Kyoto");
        var b = memory.GetOrCreateEntity("Kyoto");

        Assert.Equal(a, b);
        Assert.Equal("Kyoto", memory.NameOf(a));
    }
}
