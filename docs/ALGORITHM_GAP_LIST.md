# Topos — Algorithm Gap List (M11 input)

**Date:** 2026-07-30 · **Status:** Derived from `docs/ALGORITHM_SURVEY.md`. Companion to
`docs/ALGORITHM_SPEC.md` (which holds the must/good/no-need bucketing + impl spec). This doc is the
**concrete gap inventory**: what Topos has today vs. what the field offers, line by line, so the gap
is unambiguous and each item carries its verification tier (see `ALGORITHM_SPEC.md §3` for the oracle
strategy and this doc's §3 for the verification tiers).

**Current surface, verified against source 2026-07-30:**
`[verified:src=src/Topos.Hypergraph/IHypergraphQuery.cs, SWalk.cs, LabelPropagation.cs, TriangleCount.cs, Modularity.cs, src/Topos.Hypergraph.Knowledge/DirectedTraversal.cs]`

---

## 1. The gap inventory

### Kernel — `IHypergraphQuery` defaults + static classes

| Capability | Topos today | Field offers | Gap | Verif. tier |
|---|---|---|---|---|
| BFS / DFS | ✅ `GetBfs`/`GetDfs` | universal | — | T1 (GDS) |
| Shortest path (length + reconstructed) | ✅ `GetShortestPathLength`/`GetShortestPath` | universal | — | T1 (GDS) |
| Reachability | ✅ `IsReachable` | universal | — | T1 (GDS) |
| Connected components (WCC) | ✅ `GetConnectedComponents` | universal | — | T1 (GDS `gds.wcc`) |
| Cycle detection | ✅ `HasCycle` (with loud doc-warning for n-ary) | universal | — | T4 (unit) |
| Transitive closure | ✅ `GetTransitiveClosure` | universal | — | T4 (unit) |
| **s-walk reachability** | ✅ `SWalk.Reachable(s)` | HNX, SH.jl, xgi | partial — basic only | T2 (HNX) |
| **s-walk distance** | ✅ `SWalk.Distance(s)` | HNX, SH.jl | partial — basic only | T2 (HNX+SH.jl) |
| **s-connected-components** | ❌ | **HNX** `s_connected_components` | **GAP (must)** | T2 (HNX) |
| **s-line-graph** | ❌ | **HNX** `get_linegraph` + **xgi** `to_line_graph` | **GAP (must)** | T2 (HNX+xgi) |
| **s-diameter / s-eccentricity** | ❌ | HNX `diameter`/`edge_diameter`, SH.jl | **GAP (must)** | T2 (HNX+SH.jl) |
| **s-centralities** (betweenness/closeness/harmonic) | ❌ | **HNX** (sole impl) | GAP (good) | T2+T4 (HNX + golden) |
| **Centrality: degree** | ❌ | yamafaktory, universal | **GAP (must)** | T1 (GDS `gds.degree`) |
| **Centrality: closeness** | ❌ | yamafaktory, universal | **GAP (must)** | T1 (GDS) |
| **Centrality: betweenness** | ❌ | yamafaktory (Brandes), universal | **GAP (must)** | T1 (GDS) |
| **PageRank** | ❌ | yamafaktory (power iter), universal | **GAP (must)** | T1 (GDS `gds.pageRank`) |
| **Eigenvector centrality** (clique/H/Z-tensor) | ❌ | xgi (richest), HAT | GAP (good) | T2 (xgi) |
| **Hypergraph modularity** (native n-ary, not clique-expansion) | ❌ (has binary `Modularity.Compute`) | HNX (Kumar), HGX, SH.jl | GAP (good) — **oracle-fragmented** | T2+T5 (pick one def) |
| **Local clustering coefficient** | ❌ (has global `TriangleCount`) | xgi, JGraphT | GAP (good) | T1 (GDS `gds.localClusteringCoefficient`) |
| Label propagation | ✅ `LabelPropagation.DetectCommunities` | SH.jl, universal | — | T1 (GDS `gds.labelPropagation`) |
| Triangle counting | ✅ `TriangleCount.Count` | universal | — | T1 (GDS `gds.triangleCount`) |
| Modularity (binary-projection) | ✅ `Modularity.Compute` | universal | — | T4 (unit) |
| **k-core decomposition** | ❌ | yamafaktory `get_core` | GAP (good) | T1 (GDS `gds.kcore`) |
| **Articulation points / cut vertices** | ❌ | yamafaktory `find_cut_vertices` | GAP (good) | T1 (GDS `gds.betweenness` cut approx) |
| **Topological sort** | ❌ | yamafaktory `topological_sort` | GAP (good — for DAG role graphs) | T1 (GDS) |
| **Conductance** | ❌ | SH.jl `conductance` | GAP (low) | T4 (unit) |
| **Motif analysis** | ❌ | HGX (motifs + directed motifs) | NO-need (analytics niche) | — |
| **Spectral / eigensolver** (M7) | ❌ deferred | xgi, DHG, HyperG(R) | NO-need (no domain force) | — |
| **HGNN / GNN models** | ❌ | DHG (only real option) | NO-need (Topos is a store) | — |

### Knowledge (`Topos.Hypergraph.Knowledge`, layer-1, role-aware directed)

| Capability | Topos today | Field offers | Gap | Verif. tier |
|---|---|---|---|---|
| Directed BFS | ✅ `DirectedBfs` | (novel application) | — | T3+T4+T5 |
| Directed shortest path | ✅ `DirectedShortestPath` | (novel application) | — | T3+T4+T5 |
| Role-filtered members | ✅ `RoleFilteredMembers` | (novel) | — | T4 |
| **Directed SCC** | ❌ | yamafaktory (Kosaraju, binary) | **GAP (must)** | T1 (GDS `gds.scc` via role projection) |
| **Directed topological sort** | ❌ | yamafaktory | GAP (good) | T1 (GDS via role projection) |
| Directed PageRank | ❌ | (would be novel) | low | T3+T5 |

### Cross-cutting (not algorithm-class-specific)

| Capability | Topos today | Field offers | Gap | Verif. tier |
|---|---|---|---|---|
| **HIF interchange** (read/write) | ❌ | xgi + HNX (both `hif.py`) | GAP (good) | round-trip parity |
| **Directed hyperedges** (head/tail, not role-tagged) | role-tagged n-ary (M9) | xgi DiHypergraph, HGX | design fork — Topos chose role-tags; adequate | — |
| **Temporal hyperedges** | ❌ | HGX (first-class) | NO-need (future) | — |
| **Multiplex / signed hyperedges** | ❌ | HGX (first-class) | NO-need (future) | — |

---

## 2. The must-have shortlist (drives M11)

Six items, each with a verification oracle (full API in `ALGORITHM_SPEC.md §5`):

1. **s-connected-components** — T2 (HNX)
2. **s-line-graph** — T2 (HNX + xgi cross-check)
3. **s-diameter** — T2 (HNX + SH.jl cross-check)
4. **Directed SCC** — T1 (GDS `gds.scc` via role projection)
5. **Centrality** (degree/closeness/betweenness) — T1 (GDS)
6. **PageRank** — T1 (GDS)

**Why these six and not more:** each has (a) a real forcing consumer or strong thesis fit, AND (b)
a real verification oracle. Items without an oracle (e.g. a second modularity definition) drop to
good-to-have precisely because verification is weaker — see §3.

---

## 3. Verification tiers — the honest ladder

Topos's existing verification is **Tier 1** (single trusted oracle = GDS). The gap list introduces
Tiers 2–5 because the new algorithms include **novel-by-nature** ones GDS structurally cannot
verify. This is the honest ladder, weakest-link-first:

| Tier | Technique | What it proves | What it doesn't | When it's the best available |
|---|---|---|---|---|
| **T1 — Oracle parity** | Run vs. trusted external impl (GDS, HNX) | Output matches ground truth on shared inputs | Only works where an oracle exists | Standard algos (BFS/SCC/PageRank), s-walk family (HNX) |
| **T2 — Differential / cross-impl** | Run two *independent* impls, check agreement | Two lineages converge → strong corroboration | Doesn't prove correctness, only consistency; both could share a bug from the same paper | s-line-graph (HNX vs xgi), s-distance (HNX vs SH.jl) |
| **T3 — Metamorphic / property-based** | Test *invariants that must hold*, not specific outputs | Mathematical properties hold across random inputs | Doesn't pin the exact answer | **The key technique for genuinely novel algorithms** — see §4 |
| **T4 — Golden hand-computed** | Tiny fixed case, answer derived by hand from the definition | The definition is implemented faithfully | Small coverage; manual effort | When you ARE the oracle (novel definition) |
| **T5 — Reduction anchor** | Prove novel algo reduces to a T1-verified algo in a special case | The novel algo is correct *at least* at the anchor case | Doesn't cover the novel regime | s-walk at s=1 must equal standard BFS; directed SCC on binary edges must equal standard SCC |
| **T6 — Formal proof** | Dafny/Lean — prove the impl satisfies the spec | Mathematical certainty | Heavyweight; high cost | Load-bearing novel claims only (not in M11 scope) |

---

## 4. Can we verify novel-by-nature? — the direct answer

**Yes, but not with an oracle — with *properties* and *reductions*.** This is the substantive point.

An oracle proves "your output == ground truth." A novel-by-nature algorithm has no ground truth, so
that move is unavailable. But correctness has *other* witnesses:

### 4.1 Metamorphic testing (T3) — the workhorse for novel algorithms

Instead of "is *this* output right," test **relations that must hold between outputs regardless of
what they are.** Concrete Topos examples:

```
// s-walk family — properties that must hold for ANY correct s-distance:
1. Symmetry:      Distance(a,b,s) == Distance(b,a,s)          // undirected s-walk
2. Identity:      Distance(a,a,s) == 0
3. Triangle ineq: Distance(a,c,s) <= Distance(a,b,s) + Distance(b,c,s)
4. s-monotonicity: Components(s=2) refines Components(s=1)     // higher s only splits, never merges
5. Reachability closure: if Reachable(a,b,s) && Reachable(b,c,s) then Reachable(a,c,s)
6. s-distance monotonicity: Distance(a,b,s=2) >= Distance(a,b,s=1)  // stricter adjacency ⟹ farther

// PageRank / centrality — properties:
7. PageRank sums to 1 across all vertices
8. PageRank is invariant under relabeling (permutation invariance)
9. A node with only self-loops ranks consistently across damping values

// SCC — properties:
10. Every vertex is in exactly one SCC (partition)
11. If a in SCC(A) and b in SCC(B) and a→b exists, then either A==B or A is "upstream" of B (DAG of SCCs)
12. SCC(A) and SCC(B) disjoint or equal (no partial overlap)
```

Each of these holds for *any* correct implementation, even one with no reference. A test suite
that runs these on random hypergraphs catches most correctness bugs without any oracle. This is how
compilers verify optimizations they can't compare to a reference, and how scientific software is
tested when ground truth is expensive.

### 4.2 Reduction anchors (T5) — pin the novel regime to a verified base case

Prove your novel algorithm **degenerates to a T1-verified algorithm** in a special case. Then at
minimum, you've verified correctness at the anchor:

```
// If Topos's s-connected-components(s=1) is correct, it MUST equal
// the GDS-verified GetConnectedComponents (WCC). Test:
Assert(SComponents(graph, s=1) == GetConnectedComponents(graph));  // T5 anchor

// If Topos's DirectedScc is correct on a graph of only binary Anchor→Target edges,
// it MUST equal GDS's gds.scc on the projected binary graph. Test:
Assert(DirectedScc(roleGraph, Anchor, Target) == GdsScc(projectToBinary(roleGraph)));  // T5

// s-walk Distance(s=1) must equal GetShortestPathLength. Test:
Assert(SWalk.Distance(g, a, b, s=1) == g.GetShortestPathLength(a, b));  // T5
```

The novel regime (s=2, 3, ...; n-ary role graphs) is then covered by T3 metamorphic tests. The
combination T5+T3 is what makes novel algorithms verifiable without an oracle.

### 4.3 Differential testing (T2) — two independent lineages agreeing

Where two *independently-authored* implementations of the same definition exist, agreement is a
strong signal — especially if they have separate lineage (didn't copy each other):

- **s-line-graph:** HNX (PNNL) and xgi (Landry/Young) are independent; both cite Aksoy 2020.
  Agreement on random inputs = strong corroboration.
- **s-distance:** HNX and SimpleHypergraphs.jl (SH.jl builds s-adjacency then runs Dijkstra — a
  different algorithmic decomposition). Agreement is meaningful because the *methods differ*.

This is weaker than T1 (both could share a paper's bug) but much stronger than T4 alone.

### 4.4 What you CANNOT verify — be honest about this

- **A novel *definition*** (e.g. a 4th hypergraph modularity) has no correctness criterion beyond
  "matches the author's formula." You verify the implementation matches the paper (T4 golden), not
  that the definition is "right" — because there's no ground-truth definition. Document which paper.
- **A semantic claim** ("this traversal models how an agent recalls") is a *thesis claim*, validated
  by the real workload (does RLB/NexusVerifier behave better?), not by an oracle. That's empirical
  validation, a different epistemology from algorithmic correctness.

### 4.5 The honest per-algorithm verification plan for M11

| Algorithm | T1 | T2 | T3 | T4 | T5 | Net confidence |
|---|---|---|---|---|---|---|
| s-connected-components | — | HNX | ✅ monotonicity/closure | ✅ | ✅ s=1=WCC | **High** |
| s-line-graph | — | HNX+xgi | ✅ | ✅ | — | **High** (2 indep. impls) |
| s-diameter | — | HNX+SH.jl | ✅ bounds | ✅ | — | **High** |
| Directed SCC | GDS (role proj) | — | ✅ partition/DAG | ✅ | ✅ binary=standard SCC | **Very high** (T1 anchor) |
| Centrality (deg/close/between) | GDS | — | ✅ | ✅ | — | **Very high** |
| PageRank | GDS | — | ✅ sum=1/permutation | ✅ | — | **Very high** |
| Hypergraph modularity (if scoped) | — | HNX (Kumar only) | ✅ bounds | ✅ | ✅ strict~binary modularity | **Moderate** — definition choice, document loudly |

**Bottom line:** novel-by-nature is verifiable — not to oracle-certainty, but to a defensible
confidence via the T3+T4+T5 combination (metamorphic properties + hand-computed golden cases +
reduction to a verified base case). The s-walk family additionally gets T2 (HNX/xgi differential).
Only hypergraph modularity is genuinely under-verified, and that's because the *definition itself*
is contested — a problem of math, not engineering.
