using Topos.Hypergraph;

namespace Topos.Samples.ChatMemory;

/// <summary>
/// M5's falsifiability gate (spec §6 M5 / §6.1): the non-RLB second consumer. Everything here
/// uses only <c>Topos.Hypergraph</c>'s public API — no internal access, no kernel changes made
/// to support this domain. If something here needed a Topos change, that would be the gate
/// failing, not passing.
///
/// The domain is deliberately unlike RLB's Anchor/Condition/Target shape: conversation turns,
/// named entities, and a semantic-recall feedback loop. The one deliberate use of genuine N-ary
/// structure — <see cref="RecordMention"/> — is the actual test of the thesis spec §1 opens
/// with: one turn mentioning three entities in a single utterance is one atomic event, not three
/// independent facts, and a binary graph would have to either fragment it into three separate
/// edges (losing the "these were mentioned together, in this one turn" atomicity) or invent an
/// artificial join-node to fake N-ary-ness. Here it's just one hyperedge.
/// </summary>
public sealed class ChatMemory
{
    private readonly HypergraphKernel _kernel = new();
    private readonly PropertyKey<string> _content;
    private readonly PropertyKey<float[]> _embedding;
    private readonly PropertyKey<Provenance> _provenance;
    private readonly PropertyKey<AssertionMode> _mode;
    private readonly PropertyKey<EdgeStatistics> _recallStats;
    private readonly PropertyKey<string> _entityName;
    private readonly VectorIndex _vectorIndex;
    private readonly Dictionary<string, Handle> _entitiesByName = new(StringComparer.OrdinalIgnoreCase);

    private const byte SpeakerRole = 0, MentionedRole = 1, DerivedFromRole = 1, RecallQueryRole = 0, RecallResultRole = 1;

    public ChatMemory()
    {
        _content = _kernel.ResolveProperty<string>("content");
        _embedding = _kernel.ResolveProperty<float[]>("embedding");
        _provenance = _kernel.ResolveProperty<Provenance>("provenance");
        _mode = _kernel.ResolveProperty<AssertionMode>("mode");
        _recallStats = _kernel.ResolveProperty<EdgeStatistics>("recallStats");
        _entityName = _kernel.ResolveProperty<string>("entityName");
        _vectorIndex = new VectorIndex(_kernel, _embedding);
    }

    /// <summary>Exposes the general query surface directly — every M1 algorithm (BFS, shortest-path, connected components...) works over this domain unmodified, since they're built purely from the 5 kernel primitives.</summary>
    public IHypergraphQuery Query => _kernel;

    public Handle RecordTurn(string speaker, string content, float[] embedding, DateTimeOffset when)
    {
        var turn = _kernel.CreateVertex();
        _kernel.SetProperty(_content, turn, content);
        _kernel.SetProperty(_embedding, turn, embedding);
        _kernel.SetProperty(_provenance, turn, new Provenance(speaker, when));
        return turn;
    }

    public Handle GetOrCreateEntity(string name)
    {
        if (_entitiesByName.TryGetValue(name, out var existing)) return existing;

        var entity = _kernel.CreateVertex();
        _kernel.SetProperty(_entityName, entity, name);
        _entitiesByName[name] = entity;
        return entity;
    }

    /// <summary>
    /// One turn mentions N entities, as a single N-ary relationship — not N separate binary
    /// facts. This is the concrete instance of spec §1's thesis in a domain that has nothing to
    /// do with RLB.
    /// </summary>
    public Handle RecordMention(Handle turn, IReadOnlyList<Handle> entities)
    {
        var mention = _kernel.CreateVertex(VertexRoles.Edge);
        _kernel.AddIncidence(mention, turn, SpeakerRole, ordinal: 0);
        int ordinal = 1;
        foreach (var entity in entities)
            _kernel.AddIncidence(mention, entity, MentionedRole, ordinal++);
        return mention;
    }

    public IReadOnlyList<Handle> EntitiesMentionedIn(Handle turn) =>
        [.. _kernel.GetVertexHyperedges(turn)
            .SelectMany(edge => _kernel.IncidencesFrom(edge))
            .Where(i => i.Role == MentionedRole)
            .Select(i => i.Member)];

    /// <summary>A fact the agent extracted from a turn, reified and linked back to its source via nested reification (spec §6 M2) — structural provenance, not just a string label.</summary>
    public Handle RecordExtractedFact(Handle sourceTurn, string factDescription, AssertionMode mode)
    {
        var fact = _kernel.CreateVertex(VertexRoles.Edge);
        _kernel.SetProperty(_content, fact, factDescription);
        _kernel.SetProperty(_mode, fact, mode);
        _kernel.AddIncidence(fact, sourceTurn, DerivedFromRole, ordinal: 0);
        return fact;
    }

    public AssertionMode? ModeOf(Handle fact) =>
        _kernel.TryGetProperty(_mode, fact, out var mode) ? mode : null;

    /// <summary>Semantic recall over every turn with an embedding — the M5 vector index doing real retrieval work, not just existing as an unused class.</summary>
    public IReadOnlyList<(Handle Turn, float Distance)> RecallSimilarTurns(float[] queryEmbedding, int k) =>
        _vectorIndex.NearestNeighbors(queryEmbedding, k);

    /// <summary>Feedback loop: was a recalled turn actually useful? Accumulates via EdgeStatistics, the same pattern RLB's HyperEdge uses for its own learning loop, generalized.</summary>
    public void RecordRecallFeedback(Handle queryTurn, Handle recalledTurn, bool wasUseful)
    {
        var recall = _kernel.CreateVertex(VertexRoles.Edge);
        _kernel.AddIncidence(recall, queryTurn, RecallQueryRole, ordinal: 0);
        _kernel.AddIncidence(recall, recalledTurn, RecallResultRole, ordinal: 1);

        var current = _kernel.TryGetProperty(_recallStats, recall, out var existing) ? existing : EdgeStatistics.Initial;
        _kernel.SetProperty(_recallStats, recall, current.Observe(wasUseful));
    }

    public string? ContentOf(Handle vertex) => _kernel.TryGetProperty(_content, vertex, out var c) ? c : null;
    public string? NameOf(Handle entity) => _kernel.TryGetProperty(_entityName, entity, out var n) ? n : null;
    public Provenance? ProvenanceOf(Handle vertex) => _kernel.TryGetProperty(_provenance, vertex, out var p) ? p : null;
}
