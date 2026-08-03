using Topos.Hypergraph;
using Topos.Hypergraph.Knowledge;

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
///
/// Also a real consumer of M11 phase 1's algorithm additions (spec's M11 milestone row;
/// `docs/DECISIONS.md`'s "M11 PHASE 1 APPROVED AND IMPLEMENTED" entry): <see cref="MostConnectedEntities"/>
/// and <see cref="RankByImportance"/> use <see cref="Centrality"/>/<see cref="PageRank"/> over this
/// domain's own conversation topology, and <see cref="DetectCircularDerivations"/> uses the
/// Knowledge package's <see cref="Topos.Hypergraph.Knowledge.DirectedTraversal.DirectedScc"/> to
/// catch a genuine class of agent-memory bug (circular fact derivation) — this is an in-repo
/// example/consumer, not the cross-repo RLB/NexusVerifier adoption the spec's own exit criterion
/// (§7) still calls for.
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

    /// <summary>
    /// Roles for a two-member derivation hyperedge (<see cref="RecordDerivation"/>) — distinct
    /// from <see cref="RecordExtractedFact"/>'s single-member "which turn did this come from" link.
    /// A derivation edge is the shape <see cref="DirectedTraversal.DirectedScc"/> needs: a
    /// hyperedge with a member holding <see cref="DerivationRole.Derived"/> and a member holding
    /// <see cref="DerivationRole.Source"/>, so the M9 Knowledge package can walk Derived→Source
    /// legs directedly.
    /// </summary>
    private enum DerivationRole : byte { Derived = 0, Source = 1 }

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

    /// <summary>Records that <paramref name="derivedFact"/> was derived from <paramref name="sourceFact"/> — a directed derivation link between two extracted facts (distinct from <see cref="RecordExtractedFact"/>'s source-turn link), enabling <see cref="DetectCircularDerivations"/>.</summary>
    public Handle RecordDerivation(Handle derivedFact, Handle sourceFact)
    {
        var derivation = _kernel.CreateVertex(VertexRoles.Edge);
        _kernel.AddIncidence(derivation, derivedFact, (byte)DerivationRole.Derived, ordinal: 0);
        _kernel.AddIncidence(derivation, sourceFact, (byte)DerivationRole.Source, ordinal: 1);
        return derivation;
    }

    /// <summary>
    /// Finds circular fact derivation — an agent deriving fact A from B, B from C, and C from A
    /// is an epistemic bug (a "fact" ultimately justified only by itself), not harmless noise.
    /// M11 phase 1's <see cref="DirectedTraversal.DirectedScc"/> (spec's Knowledge layer) finds
    /// every strongly-connected component over the Derived→Source adjacency; every non-cyclic fact
    /// lands in its own singleton component (<c>DirectedScc</c>'s documented "every vertex,
    /// including singletons" convention), so filtering to components with more than one member
    /// isolates genuine derivation cycles. This generalizes the class of hand-rolled cycle guards
    /// `docs/NEXUS_VERIFIER_INTEGRATION_FINDINGS.md` finding #4 describes, for this domain's own
    /// derivation shape rather than NexusVerifier's Anchor/Target one.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<Handle>> DetectCircularDerivations() =>
        [.. _kernel.DirectedScc((byte)DerivationRole.Derived, (byte)DerivationRole.Source)
            .Where(component => component.Count > 1)];

    /// <summary>
    /// Ranks entities by topological degree (<see cref="Centrality.Degree"/>, M11 phase 1) — how
    /// many distinct turns/entities each co-occurs with under the shared mention-hyperedge
    /// topology. This is connectedness, not raw mention count: an entity mentioned three times
    /// alongside the same other entity scores lower than one mentioned twice alongside two
    /// different entities. Useful for e.g. surfacing which named entities anchor the most distinct
    /// parts of a conversation.
    /// </summary>
    public IReadOnlyList<(Handle Entity, int Degree)> MostConnectedEntities(int topK) =>
        [.. Centrality.Degree(_kernel)
            .Where(kv => _entitiesByName.ContainsValue(kv.Key))
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key.Index)
            .Take(topK)
            .Select(kv => (kv.Key, kv.Value))];

    /// <summary>
    /// PageRank (M11 phase 1) over the whole conversation graph — turns, entities, and reified
    /// facts alike — as the standard iterative "what's load-bearing" signal. A distribution
    /// summing to 1.0 across every vertex; useful for e.g. deciding what to keep first when
    /// trimming an over-long memory.
    /// </summary>
    public IReadOnlyDictionary<Handle, double> RankByImportance() => PageRank.Compute(_kernel);

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
