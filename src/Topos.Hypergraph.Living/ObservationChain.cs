namespace Topos.Hypergraph.Living;

/// <summary>
/// Layer-1 timestamped observation-chain / state-history convenience over
/// <see cref="IHypergraphQuery"/>/<see cref="HypergraphKernel"/> (docs/DECISIONS.md's "M12 SCOPED
/// AND IMPLEMENTED" entry). <see cref="PropertyKey{T}"/> is present-only — a
/// <see cref="HypergraphKernel.SetProperty{T}"/> call overwrites the previous value — so there is
/// no built-in way to ask "what was this entity's state at an earlier point, and how did it
/// change." This class is the composition pattern that answers that, using nothing but existing
/// primitives: each state-change is reified as its own edge-vertex (<see cref="VertexRoles.Edge"/>),
/// incident to the entity it describes and, except at genesis, to its immediate predecessor — a
/// backward-linked list of snapshots per entity (the "edges as vertices" reification pattern,
/// spec §7 pattern 12, applied to time). No new kernel storage.
///
/// Deliberately does not offer a "find the latest observation for this entity" method — see
/// <see cref="ObservationHistory"/>'s doc for why, and docs/USAGE_PATTERNS.md Pattern 10 for the
/// recommended head-tracking alternative.
/// </summary>
public static class ObservationChain
{
    /// <summary>
    /// This package's own <see cref="PropertyKey{T}"/> name for the timestamp every observation
    /// is stamped with. Fixed and package-owned (unlike role bytes, which stay caller-supplied)
    /// because every consumer needs *a* timestamp for history to mean anything, and only a single
    /// known name lets <see cref="RecordedAt"/> read it back generically across consumers.
    /// </summary>
    private const string RecordedAtPropertyName = "topos.living.recordedAt";

    /// <summary>
    /// Records one state-change as a new reified observation vertex, incident to
    /// <paramref name="entity"/> under <paramref name="entityRole"/> (ordinal 0) and, when
    /// <paramref name="previous"/> is given, to that prior observation under
    /// <paramref name="previousRole"/> (ordinal 1). Stamps <paramref name="recordedAt"/> under
    /// this package's own <see cref="PropertyKey{T}"/>, readable back via <see cref="RecordedAt"/>.
    ///
    /// Returns the new observation's <see cref="Handle"/>. The caller is responsible for
    /// remembering it as the new "head" of the chain (e.g. in an ordinary
    /// <c>PropertyKey&lt;Handle&gt;</c> on <paramref name="entity"/>) — this method does not track
    /// or update any notion of "current" itself; see <see cref="ObservationHistory"/>'s doc.
    ///
    /// Attach whatever additional state the domain needs (a position, an
    /// <see cref="EdgeStatistics"/> confidence, an <see cref="AssertionMode"/>, a
    /// <see cref="LearnableEdge"/>, anything) to the returned Handle via ordinary
    /// <see cref="HypergraphKernel.SetProperty{T}"/> calls — this method only handles the chaining
    /// and the timestamp.
    /// </summary>
    public static Handle RecordObservation(
        this HypergraphKernel kernel,
        Handle entity, byte entityRole,
        Handle? previous, byte previousRole,
        DateTimeOffset recordedAt)
    {
        var observation = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(observation, entity, entityRole, ordinal: 0);
        if (previous is { } previousHandle)
            kernel.AddIncidence(observation, previousHandle, previousRole, ordinal: 1);

        var recordedAtKey = kernel.ResolveProperty<DateTimeOffset>(RecordedAtPropertyName);
        kernel.SetProperty(recordedAtKey, observation, recordedAt);
        return observation;
    }

    /// <summary>
    /// The <see cref="DateTimeOffset"/> <see cref="RecordObservation"/> stamped on
    /// <paramref name="observation"/>, or null if <paramref name="observation"/> was never
    /// created via <see cref="RecordObservation"/>.
    /// </summary>
    public static DateTimeOffset? RecordedAt(this HypergraphKernel kernel, Handle observation)
    {
        var recordedAtKey = kernel.ResolveProperty<DateTimeOffset>(RecordedAtPropertyName);
        return kernel.TryGetProperty(recordedAtKey, observation, out var value) ? value : null;
    }

    /// <summary>
    /// One-hop: the entity <paramref name="observation"/> describes — the member holding
    /// <paramref name="entityRole"/>. Null if none found (not a genuine observation vertex, or a
    /// malformed call site). If more than one member holds the role, returns the first per
    /// <see cref="IHypergraphQuery.GetHyperedgeVertices"/>'s iteration order — this class does not
    /// validate cardinality, matching the kernel's own "does not judge" stance on
    /// <see cref="Incidence.Role"/> (docs/ROLE_CONVENTIONS.md).
    /// </summary>
    public static Handle? EntityOfObservation(this IHypergraphQuery graph, Handle observation, byte entityRole)
    {
        foreach (var incidence in graph.GetHyperedgeVertices(observation))
        {
            if (incidence.Role == entityRole) return incidence.Member;
        }
        return null;
    }

    /// <summary>
    /// One-hop: <paramref name="observation"/>'s immediate predecessor — the member holding
    /// <paramref name="previousRole"/>, or null at genesis (no predecessor was linked).
    /// </summary>
    public static Handle? PreviousObservation(this IHypergraphQuery graph, Handle observation, byte previousRole)
    {
        foreach (var incidence in graph.GetHyperedgeVertices(observation))
        {
            if (incidence.Role == previousRole) return incidence.Member;
        }
        return null;
    }

    /// <summary>
    /// Lazily walks an entity's history <b>newest → oldest</b>, starting at
    /// <paramref name="latest"/> (included) and following <see cref="PreviousObservation"/> until
    /// genesis. Streaming (<c>yield return</c>), so a caller can stop early (e.g. <c>.Take(5)</c>
    /// or <c>.TakeWhile(...)</c>) without materializing the whole chain. A caller wanting
    /// chronological order calls <c>.Reverse()</c> on a materialized list — an explicit cost, not
    /// something this method hides, since a backward-linked list cannot walk forward without
    /// buffering the whole chain first.
    ///
    /// <b>No "find the latest observation" method exists on this class, deliberately.</b> "Latest"
    /// has no unambiguous answer from topology alone: it would need either an O(n) reverse-index
    /// scan this class would have to build and maintain, or a fragile timestamp tie-break (clock
    /// skew, replayed/imported history, or two observations sharing a timestamp all make it
    /// ambiguous). Track the head yourself instead — an ordinary <c>PropertyKey&lt;Handle&gt;</c>
    /// on the entity, updated on every <see cref="RecordObservation"/> call, costs no new API
    /// surface and round-trips through <c>Topos.Hypergraph.Persistence</c> for free since it's just
    /// another property. See docs/USAGE_PATTERNS.md Pattern 10.
    ///
    /// Throws <see cref="InvalidOperationException"/> if a cycle is detected — a well-formed
    /// observation chain (one built entirely via <see cref="RecordObservation"/>) never revisits a
    /// vertex, so a cycle means the chain was constructed some other way and is malformed.
    /// </summary>
    public static IEnumerable<Handle> ObservationHistory(this IHypergraphQuery graph, Handle latest, byte previousRole)
    {
        var visited = new HashSet<Handle>();
        var current = latest;
        while (true)
        {
            if (!visited.Add(current))
            {
                throw new InvalidOperationException(
                    $"ObservationHistory detected a cycle at {current} — a well-formed observation " +
                    "chain (built via RecordObservation) never revisits a vertex.");
            }
            yield return current;

            var previous = graph.PreviousObservation(current, previousRole);
            if (previous is not { } previousHandle) yield break;
            current = previousHandle;
        }
    }
}
