namespace Topos.Hypergraph;

/// <summary>
/// M1's query surface (spec §6 M1): a small set of required primitives, plus algorithms
/// default-implemented purely in terms of those primitives — yamafaktory's
/// <c>HypergraphQuery&lt;V,HE&gt;</c> trait pattern, "the best API-factoring idea in any library
/// analyzed" (`BASE_INVESTIGATION.md §3.3`), adapted to C# default interface methods.
///
/// <b>Adapted, not copied.</b> Verified directly against yamafaktory's actual
/// <c>trait_def.rs</c> (not just the investigation's summary) on 2026-07-24: yamafaktory's trait
/// has exactly 9 required primitives and ~45 default-implemented algorithms, generic over
/// separate vertex-weight (<c>V</c>) and hyperedge-weight (<c>HE</c>) types, with hyperedges as a
/// first-class ordered-vertex-list concept distinct from vertices.
///
/// Topos's shape is different by design: a hyperedge is a <see cref="Vertex"/> tagged
/// <see cref="VertexRoles.Edge"/> (the Role:Edge reification pattern, spec §7 pattern 12), and
/// typed data attaches via <see cref="PropertyKey{T}"/> pools rather than one opaque weight per
/// vertex/edge (spec §3, primitive 4). So this interface has no <c>get_vertex_weight</c> /
/// <c>get_hyperedge_weight</c> analogue — <see cref="TryGetVertex"/> exposes the full
/// <see cref="Vertex"/> record instead, and property access goes through
/// <see cref="HypergraphKernel.TryGetProperty{T}"/> directly. The primitive *count* differs from
/// yamafaktory's 9 as a result (5 required here, not 9) — that's a consequence of the reification
/// design, not a shortfall against the borrowed pattern; the remaining 4 of yamafaktory's 9
/// (<c>is_empty</c>, one weight getter, and the two "vertices of size N" derivations) become
/// derivable defaults here instead of requirements.
///
/// M1's roadmap row (spec §6) names six algorithms: BFS, DFS, reachable, shortest-path, cycle,
/// transitive closure, plus SCC. All six land here except SCC, which is deliberately *not*
/// offered as its own method — see <see cref="GetConnectedComponents"/>'s doc for why: the
/// topology-only adjacency every method here shares is inherently symmetric (co-incidence on a
/// shared hyperedge, not the stored Incidence direction), so "strongly connected" and "weakly
/// connected" coincide at this layer. A genuinely directed SCC needs role-awareness (only
/// following Anchor→Target legs, say), which is explicitly a layer-1 concern this kernel-level
/// interface doesn't have (spec §4.1: "the kernel does not judge").
///
/// GDS-parity coverage (spec §5, `tests/Topos.Tests.GdsOracle`): BFS, DFS, and
/// <see cref="GetConnectedComponents"/> each have a direct GDS oracle (`gds.bfs`, `gds.dfs`,
/// `gds.wcc`) and are verified against it. <see cref="GetShortestPathLength"/> is verified too,
/// with a documented correction for the bipartite-vs-logical hop-count relationship. Cycle
/// detection and transitive closure are **not** standard GDS procedures — spec §5's own honest
/// limit ("where the hypergraph and its projection disagree, GDS cannot verify") applies; those
/// two are unit-tested only, on hand-verified small graphs.
///
/// <b>Role-aware/directed traversal stays out of this interface — confirmed, not just deferred,
/// as of M8.</b> Every default algorithm here (<see cref="GetBfs"/>, <see cref="GetDfs"/>,
/// <see cref="GetShortestPath"/>, <see cref="GetConnectedComponents"/>, <see cref="HasCycle"/>)
/// is role-blind and co-membership-symmetric by design (spec §4.1: "the kernel does not judge").
/// Two independent real consumers (`samples/Topos.Samples.ChatMemory`'s
/// <c>EntitiesMentionedIn</c>, and NexusVerifier's n-ary proof-search chainer — see
/// `docs/NEXUS_VERIFIER_INTEGRATION_FINDINGS.md` finding #5) have each hand-rolled the same
/// ~10-line role-filtered walk over <see cref="HypergraphKernel.IncidencesFrom"/> +
/// <see cref="Incidence.Role"/>. M8 does not add that here — it stays a layer-1 concern. If a
/// future layer-1 <c>Topos.Hypergraph.Knowledge</c> package (M9+) materializes, directed/
/// role-gated search (e.g. <c>DirectedBfs(start, roleFilter)</c>) is its most obvious candidate,
/// precisely because two unrelated consumers already needed the same thing.
/// </summary>
public interface IHypergraphQuery
{
    // ── Required primitives ─────────────────────────────────────────────────

    /// <summary>Total vertex count, including dormant ones (Invariant 1: dormant is never deleted).</summary>
    int CountVertices();

    /// <summary>Every vertex Handle, including dormant ones. A snapshot, not a live view.</summary>
    IReadOnlyList<Handle> VertexHandles();

    bool TryGetVertex(Handle handle, out Vertex vertex);

    /// <summary>Which hyperedges this vertex is a member of — the distinct Source handles across its incidence memberships.</summary>
    IReadOnlyList<Handle> GetVertexHyperedges(Handle vertex);

    /// <summary>
    /// Which vertices participate in this hyperedge, and under what role/ordinal — the full
    /// <see cref="Incidence"/> records, not just bare handles (a deliberate divergence from
    /// yamafaktory's plain <c>Vec&lt;VertexIndex&gt;</c>: Topos's role-tagged, ordinal-ordered
    /// N-ary structure is the whole point, per spec §1, so the query surface exposes it).
    /// </summary>
    IReadOnlyList<Incidence> GetHyperedgeVertices(Handle hyperedge);

    // ── Default-implemented derivations ─────────────────────────────────────

    bool IsEmpty() => CountVertices() == 0;

    bool ContainsVertex(Handle handle) => TryGetVertex(handle, out _);

    Vertex GetVertex(Handle handle) =>
        TryGetVertex(handle, out var v) ? v : throw new KeyNotFoundException($"No vertex for {handle}");

    /// <summary>
    /// Every hyperedge Handle — vertices tagged <see cref="VertexRoles.Edge"/>. O(V) by scanning
    /// <see cref="VertexHandles"/>; a dedicated index would make this O(E), which is worth adding
    /// once a real workload's edge:vertex ratio makes the scan cost measurable (same
    /// measure-before-optimizing discipline as the M0 benchmark gate).
    /// </summary>
    IReadOnlyList<Handle> HyperedgeHandles()
    {
        var result = new List<Handle>();
        foreach (var h in VertexHandles())
        {
            if (TryGetVertex(h, out var v) && v.Roles.HasFlag(VertexRoles.Edge))
                result.Add(h);
        }
        return result;
    }

    int CountHyperedges() => HyperedgeHandles().Count;

    // ── Default-implemented algorithms (first slice) ────────────────────────

    /// <summary>
    /// Breadth-first traversal from <paramref name="start"/>, treating any two vertices
    /// co-incident on the same hyperedge as adjacent (a topology-only reading — role-gated
    /// traversal, e.g. only following Anchor→Target legs, is a layer-1 Knowledge-model concern
    /// per spec §4.1, not something the kernel-level query surface judges). Built entirely from
    /// <see cref="GetVertexHyperedges"/> and <see cref="GetHyperedgeVertices"/>, per the trait
    /// pattern's whole point: primitives in, algorithm out, no direct storage access needed.
    /// </summary>
    IEnumerable<Handle> GetBfs(Handle start)
    {
        if (!ContainsVertex(start)) yield break;

        var visited = new HashSet<Handle> { start };
        var queue = new Queue<Handle>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;

            foreach (var edge in GetVertexHyperedges(current))
            {
                foreach (var incidence in GetHyperedgeVertices(edge))
                {
                    if (visited.Add(incidence.Member))
                        queue.Enqueue(incidence.Member);
                }
            }
        }
    }

    bool IsReachable(Handle from, Handle to) => GetBfs(from).Any(h => h == to);

    /// <summary>
    /// Depth-first traversal from <paramref name="start"/>, same topology-only adjacency as
    /// <see cref="GetBfs"/>. Iterative (mark-on-push), not recursive, to avoid stack-depth limits
    /// on large graphs — this gives a valid DFS exploration order (depth before backtrack), not
    /// necessarily byte-identical to every other DFS implementation's tie-breaking when a vertex
    /// has multiple unvisited neighbors (only the *reachable set*, not the exact sequence, is a
    /// cross-implementation invariant — see `BfsGdsParityTests`'s sibling DFS test).
    /// </summary>
    IEnumerable<Handle> GetDfs(Handle start)
    {
        if (!ContainsVertex(start)) yield break;

        var visited = new HashSet<Handle> { start };
        var stack = new Stack<Handle>();
        stack.Push(start);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            foreach (var edge in GetVertexHyperedges(current))
            {
                foreach (var incidence in GetHyperedgeVertices(edge))
                {
                    if (visited.Add(incidence.Member))
                        stack.Push(incidence.Member);
                }
            }
        }
    }

    /// <summary>
    /// Hop-count shortest-path length from <paramref name="from"/> to <paramref name="to"/> in
    /// the topology-only adjacency — null if unreachable, 0 if <paramref name="from"/> ==
    /// <paramref name="to"/>. One "hop" is one vertex-to-vertex step (crossing one hyperedge),
    /// not one bipartite vertex→edge→vertex pair — <c>BfsGdsParityTests</c>'s GDS comparison has
    /// to halve GDS's raw bipartite hop count to compare against this, since GDS walks the
    /// projected vertex+edge-node graph directly and doesn't know Topos's convention that
    /// edge-nodes are traversal scaffolding, not hops.
    /// </summary>
    int? GetShortestPathLength(Handle from, Handle to)
    {
        if (!ContainsVertex(from) || !ContainsVertex(to)) return null;
        if (from == to) return 0;

        var visited = new HashSet<Handle> { from };
        var queue = new Queue<(Handle Vertex, int Distance)>();
        queue.Enqueue((from, 0));

        while (queue.Count > 0)
        {
            var (current, distance) = queue.Dequeue();
            foreach (var edge in GetVertexHyperedges(current))
            {
                foreach (var incidence in GetHyperedgeVertices(edge))
                {
                    if (incidence.Member == to) return distance + 1;
                    if (visited.Add(incidence.Member))
                        queue.Enqueue((incidence.Member, distance + 1));
                }
            }
        }
        return null;
    }

    /// <summary>
    /// One shortest path from <paramref name="from"/> to <paramref name="to"/>, as the ordered
    /// vertex sequence including both endpoints — empty if unreachable, <c>[from]</c> if
    /// <paramref name="from"/> == <paramref name="to"/>. BFS with parent-pointer reconstruction;
    /// when multiple shortest paths tie, which one comes back depends on
    /// <see cref="GetHyperedgeVertices"/>'s iteration order — only
    /// <see cref="GetShortestPathLength"/>'s *length* is a cross-implementation invariant, not
    /// this specific path.
    /// </summary>
    IReadOnlyList<Handle> GetShortestPath(Handle from, Handle to)
    {
        if (!ContainsVertex(from) || !ContainsVertex(to)) return [];
        if (from == to) return [from];

        var parent = new Dictionary<Handle, Handle>();
        var queue = new Queue<Handle>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var edge in GetVertexHyperedges(current))
            {
                foreach (var incidence in GetHyperedgeVertices(edge))
                {
                    var next = incidence.Member;
                    if (next == from || parent.ContainsKey(next)) continue;

                    parent[next] = current;
                    if (next == to) return ReconstructPath(parent, from, to);
                    queue.Enqueue(next);
                }
            }
        }
        return [];
    }

    private static IReadOnlyList<Handle> ReconstructPath(Dictionary<Handle, Handle> parent, Handle from, Handle to)
    {
        var path = new List<Handle> { to };
        var current = to;
        while (current != from)
        {
            current = parent[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    /// <summary>
    /// <b>Warning, read this before calling: on any real n-ary hypergraph (3+ members on a
    /// hyperedge) this returns <c>true</c> almost always. It is not a general "is this hypergraph
    /// acyclic" check</b> — see below for why, and use role-aware traversal at layer 1 for the
    /// question you probably actually want.
    ///
    /// Whether the topology-only adjacency graph contains a cycle, checking every component
    /// (not just one). Standard undirected-graph DFS cycle detection: track each vertex's parent
    /// in the DFS tree; a visited neighbor that isn't the immediate parent means a cycle.
    ///
    /// <b>Why:</b> under the clique-style adjacency this method uses, any hyperedge with 3+
    /// members is *trivially* cyclic — three co-members A, B, C are all pairwise "adjacent" (each
    /// is co-incident with the other two on the same hyperedge), so A→B→C→A is a real cycle in
    /// the derived graph even though there's only one hyperedge involved. RLB's own D2 hyperedges
    /// (Anchor + Conditions + Target) almost always have 3+ members, so <see cref="HasCycle"/>
    /// will return <c>true</c> for nearly any real RLB-shaped graph — that's correct given what
    /// this method actually computes (cycles in the clique-expansion of hyperedge membership),
    /// not a bug, but it means "is this hypergraph acyclic" is a near-always-false question at
    /// this layer. Answering the more useful question ("is there a cyclic *dependency* among
    /// Anchor→Target legs") needs role-aware traversal — a layer-1 concern, not this method's job
    /// (confirmed M8, not adding it to the kernel — see `docs/DECISIONS.md`).
    ///
    /// <b>This is not hypothetical: a real second consumer hit exactly this.</b> NexusVerifier's
    /// AND-OR proof-search chainer considered using <see cref="HasCycle"/> for its own cycle
    /// detection, read this doc, correctly backed off, and hand-rolled a per-DFS-path
    /// <c>HashSet</c> guard instead (`docs/NEXUS_VERIFIER_INTEGRATION_FINDINGS.md` finding #4).
    /// The doc did its job that time — keep it loud so it keeps doing that job. See
    /// <c>CycleDetectionTests</c> for both the trivial-3-member case and a genuinely acyclic
    /// binary-chain case.
    /// </summary>
    bool HasCycle()
    {
        var visited = new HashSet<Handle>();
        foreach (var start in VertexHandles())
        {
            if (visited.Contains(start)) continue;
            if (HasCycleFrom(start, Handle.Invalid, visited)) return true;
        }
        return false;
    }

    private bool HasCycleFrom(Handle current, Handle parent, HashSet<Handle> visited)
    {
        visited.Add(current);
        foreach (var edge in GetVertexHyperedges(current))
        {
            foreach (var incidence in GetHyperedgeVertices(edge))
            {
                var next = incidence.Member;
                if (next == current) continue; // a self-referential incidence isn't a topology cycle

                if (!visited.Contains(next))
                {
                    if (HasCycleFrom(next, current, visited)) return true;
                }
                else if (next != parent)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// For every vertex, every other vertex reachable from it (topology-only adjacency) — the
    /// full reachability closure. O(V·(V+E)): one BFS per vertex. Fine for M1's correctness-first
    /// scope; a real workload needing this at scale would want an incremental/cached version
    /// (measure first, per the M0 benchmark-gate discipline, before building that machinery).
    /// Not GDS-verified — transitive closure isn't a standard GDS procedure (spec §5's honest
    /// limit); <c>TransitiveClosureTests</c> cross-checks it against <see cref="IsReachable"/>
    /// pairwise instead, which is a real (if narrower) correctness signal.
    /// </summary>
    IReadOnlyDictionary<Handle, IReadOnlyList<Handle>> GetTransitiveClosure()
    {
        var result = new Dictionary<Handle, IReadOnlyList<Handle>>();
        foreach (var v in VertexHandles())
            result[v] = [.. GetBfs(v).Where(h => h != v)];
        return result;
    }

    /// <summary>
    /// Partitions *every* vertex — domain and hyperedge alike — into connected components over
    /// the full bipartite structure. This is what spec §6 M1's roadmap calls "SCC," deliberately
    /// relabeled: because the graph this interface exposes is inherently symmetric at the
    /// topology level (co-incidence on a hyperedge, not the stored Source→Member direction),
    /// strongly- and weakly-connected components are the same thing here — there's no asymmetry
    /// for "strongly" to mean anything beyond "weakly" without role-aware (layer-1) traversal.
    /// GDS-verified against <c>gds.wcc</c> (Weakly Connected Components), which is the honest
    /// match for what this actually computes.
    ///
    /// <b>Deliberately does not reuse <see cref="GetBfs"/>.</b> <see cref="GetBfs"/>/
    /// <see cref="GetDfs"/> treat hyperedge-vertices as pure lookup scaffolding and never yield
    /// them — which is correct for "what domain vertices can I reach," but wrong here: it would
    /// silently strand every hyperedge as its own singleton component, since nothing else's
    /// traversal ever visits it. This walks both bipartite directions explicitly
    /// (<see cref="GetVertexHyperedges"/> *and* <see cref="GetHyperedgeVertices"/> from every
    /// node, harmless no-ops in whichever direction doesn't apply) so hyperedge-vertices land in
    /// the same component as their members — matching what <c>gds.wcc</c> computes over the same
    /// bipartite projection. Found by a failing test, not designed in up front — see
    /// `TransitiveClosureAndComponentsTests`.
    /// </summary>
    IReadOnlyList<IReadOnlyList<Handle>> GetConnectedComponents()
    {
        var components = new List<IReadOnlyList<Handle>>();
        var visited = new HashSet<Handle>();

        foreach (var start in VertexHandles())
        {
            if (visited.Contains(start)) continue;

            var component = new List<Handle>();
            var queue = new Queue<Handle>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);

                foreach (var edge in GetVertexHyperedges(current))
                {
                    if (visited.Add(edge)) queue.Enqueue(edge);
                }
                foreach (var incidence in GetHyperedgeVertices(current))
                {
                    if (visited.Add(incidence.Member)) queue.Enqueue(incidence.Member);
                }
            }
            components.Add(component);
        }
        return components;
    }
}
