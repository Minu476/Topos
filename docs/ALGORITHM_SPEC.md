# Topos — Algorithm Completeness Spec (M11 proposal)

**Status:** 🟡 PROPOSED — Nasser's go/no-go. Authored 2026-07-30 by GLM-5.2 (documentation role)
from a fresh independent survey of hypergraph libraries across all major languages
(`docs/ALGORITHM_SURVEY.md` holds the full survey; this spec derives from it). If approved, a
code-authoring session (Opus 5) executes §5. This is a *proposal*, not an applied change.

**Integrity standard:** claims carry `[verified:src=...]` / `[verified:web=...]` /
`[verified:spec=...]` tags per `BASE_INVESTIGATION.md §8`. Net-new algorithm APIs below are
`[unverified:proposed]` until implemented and tested.

---

## 1. Why this spec exists — the gap this closes

Topos ships a solid standard-algorithm backbone (M1 traversal, M6 analytics), all
**GDS-verified** against Neo4j for the binary-projection-verifiable set (BFS/DFS/shortest-path/
WCC/label-propagation/triangle-count). M9 added role-aware **directed** traversal. But a fresh
multi-language survey (`docs/ALGORITHM_SURVEY.md`) shows three real gaps measured against Topos's
own thesis (AI/agent memory), plus a small set of "nice but not on-thesis" items. This spec
proposes the on-thesis gaps as **M11**, each with a concrete verification oracle (GDS for the
standard ones, **HyperNetX as a new oracle for the hypergraph-native ones GDS cannot check**).

**The thesis test for each proposed algorithm:** does it serve *long-lived, mutable, explainable
agent memory* — retrieval, ranking, clustering, or consistency checking? If yes, must-have. If it
serves a different workload (VLSI partitioning, deep-learning training, pure topology research),
no-need.

---

## 2. The bucketing (judged for Topos specifically)

### MUST-have — gaps that serve the thesis, with a verification oracle

| Capability | Why it matters for AI memory | Oracle | Package home |
|---|---|---|---|
| **s-connected-components** | "which memories are in the same s-connected island" — retrieval boundary | **HyperNetX** (`s_connected_components`) | Kernel |
| **s-line-graph** | The substrate s-centrality/s-distance reduce onto; also a "related edges" view | **HyperNetX** + **xgi** (`to_line_graph`) cross-check | Kernel |
| **s-diameter / s-eccentricity** | Bounded "how far apart can two memories be" — informs retrieval depth | **HyperNetX** (`diameter`/`edge_diameter`) | Kernel |
| **Directed SCC** | Detect cyclic dependencies among Anchor→Target legs (NexusVerifier hand-rolled this — finding #4) | GDS `gds.scc` (via role projection) | **Knowledge** (layer-1, role-aware) |
| **Centrality: degree / closeness / betweenness** | "which memory is most central / on the most paths" — ranking & importance | GDS (`gds.degree`, closeness, betweenness) | Kernel |
| **PageRank** | Iterative importance over the memory graph; the standard "what's load-bearing" signal | GDS (`gds.pageRank`) | Kernel |

### GOOD-to-have — real value, but either lower-priority or oracle-fragmented

| Capability | Why it's good-to-have, not must | Oracle situation |
|---|---|---|
| **Hypergraph modularity** (one definition) | Clustering related memories into communities. **3+ competing peer-reviewed definitions** (Kumar, Chodrow-Veldt-Benson, Kamiński h-Louvain) — no canonical oracle. | Pick one, cite it, cross-check the matching repo only. See §6.2. |
| **HIF interchange** (read/write) | Cross-tool portability — both XGI and HNX support the Hypergraph Interchange Format. Aligns Topos with an emerging standard. | Round-trip test against xgi/HNX output. |
| **Local clustering coefficient** | Per-vertex "how cliquey is this memory's neighborhood." Topos has triangle count but not LCC. | GDS (`gds.localClusteringCoefficient`). |

### NO-need (against the thesis) — explicitly out of scope

| Capability | Why no-need | Held by |
|---|---|---|
| **Full spectral decomposition / eigensolvers** (deferred M7) | No domain force yet. Spectral *builders* (Laplacian) may land as a byproduct of modularity, but a general eigensolver is scope creep. | xgi, DHG, HyperG(R) |
| **Hypergraph neural networks (HGNN, HGNN+, etc.)** | Topos is a **store**, not a learning framework. DHG owns this niche; Topos provides the primitives a GNN would read from. | DHG (DeepHypergraph) |
| **Simplicial complexes / homology / topological TDL** | Different mathematical object (down-closed simplex sets). Niche; not agent-memory-shaped. | TopoNetX, HNX homology |
| **Concept lattices (Formal Concept Analysis)** | Niche analytical capability. No agent-memory pull. | HyperNetX only |
| **Hypergraph partitioning (KaHyPar-style)** | VLSI/SAT workload, static combinatorial object — opposite of memory's mutable n-ary relations. Steal the *storage pattern* (CSR), not the algorithm. | KaHyPar, Mt-KaHyPar |

> **Note:** "no-need" means *not a milestone target*. The primitives Topos ships (reification,
> n-ary edges, typed properties) don't *block* any of these — a consumer could build an HGNN on
> Topos, or run concept-lattice analysis over it. They're just not where Topos invests its own
> algorithm surface.

---

## 3. The new oracle — HyperNetX (and why)

Topos's existing oracle is **Neo4j GDS** — a *binary* graph library. It cannot verify
hypergraph-native algorithms (s-walk family, hypergraph modularity), because those semantics don't
exist in a binary projection. The survey found a strong answer for the s-walk family specifically:

**HyperNetX (HNX) is the canonical oracle for the s-walk family**, and the reason is unusually
clean: the s-walk framework was *defined* by Aksoy, Joslyn, Purvine, Praggastis (EPJ Data Science
2020, "Hypernetwork Science via High-Order Hypergraph Walks"), and **the same people wrote
HyperNetX**. Competing libraries explicitly defer to it — SimpleHypergraphs.jl's source contains
the comment *"The concepts of s-distance and s-walk have been defined in the Python library
HyperNetX"* `[verified:src=pszufe/SimpleHypergraphs.jl/src/algorithms/distance.jl]`. This is the
closest the field has to a Lean-style trusted reference for these algorithms.

**Coverage:** HNX implements the full s-walk family with tests — `get_linegraph(s=)`,
`s_connected_components(s=)`, `distance`/`edge_distance(s=)`, `diameter`/`edge_diameter(s=)`, plus
the s-centrality suite (`s_betweenness`, `s_closeness`, `s_harmonic`, `s_eccentricity`).
`[verified:src=pnnl/HyperNetX/classes/hypergraph.py, algorithms/metrics/s_centrality_measures.py]`

**License:** 3-Clause BSD (GitHub reports `NOASSERTION` only because the LICENSE is `.rst`; the
SPDX identifier in `pyproject.toml` is BSD-3-Clause). Test-only use via subprocess/Docker — same
isolation pattern as the existing GDS oracle (`docs/GDS_ORACLE_SETUP.md`).

**Where HNX is NOT enough — the modularity caveat:** hypergraph modularity has **3+ competing,
mutually-inconsistent peer-reviewed definitions** (Kumar et al. linear/majority/strict purity;
Chodrow-Veldt-Benson AON/differentiated objective; Kamiński h-Louvain). HNX ships only the Kumar
formulation. **There is no single canonical oracle for hypergraph modularity** — see §6.2.

**Cross-check oracle — xgi** (`xgi-org/xgi`): a second, independent, peer-reviewed implementation
of s-line-graph (`xgi.convert.line_graph(H, s=1)`) and the tensor/eigenvector centrality family.
Agreement between HNX and xgi on s-line-graph is meaningful because they're independent
implementations of the same cited definition.

### 3.1 HNX oracle harness — a new test project

Mirror the existing GDS-oracle pattern (`tests/Topos.Tests.GdsOracle/`):

- **New project:** `tests/Topos.Tests.HnxOracle/` — a .NET test project that drives HNX over a
  subprocess (or a Docker sidecar `topos-hnx-oracle`, Python 3.10+, `pip install hypergraphx` —
  wait, HNX: `pip install hypergraphx` is wrong; it's `pip install hypernetx`).
- **Same soft-fail convention as GDS-oracle:** if the HNX sidecar isn't reachable, tests skip
  gracefully (pass trivially), so the suite never breaks CI or another developer's machine.
  `[verified:src=tests/Topos.Tests.GdsOracle — Neo4jTestConfig.TryLoad() soft-fail pattern]`
- **Credential isolation:** HNX is local-only, no credentials. Simpler than the GDS oracle (which
  has the documented host-instance isolation concern). Disposable container, own ports.

---

## 4. What stays where — the layer discipline (🔒 LOCKED architecture)

This spec respects Topos's locked layer split (`docs/SPECIFICATION.md §4.1`):

- **Kernel (`Topos.Hypergraph`):** topology-only, role-blind, co-membership-symmetric ("the kernel
  does not judge"). All s-walk variants, centrality, and PageRank land here — they're
  topology-only by definition.
- **Knowledge (`Topos.Hypergraph.Knowledge`, the M9 layer-1 package):** role-aware, directed.
  **Directed SCC lands here** — it's the directed counterpart to the kernel's WCC, exactly as
  DirectedBfs/DirectedShortestPath are the directed counterparts to the kernel's BFS/shortest-path.

Nothing in this spec touches the four-primitive storage contract (Handle/Vertex/Incidence/
PropertyKey) or the two invariants. Every algorithm is built on `IHypergraphQuery` (kernel) or the
M9 role-aware extension surface (Knowledge) — the trait pattern, same as M1/M6/M9.

---

## 5. The implementation spec (per-algorithm, for Opus 5)

Each item specifies: API signature (matching Topos's style), package home, oracle, tests,
complexity, and provenance (which library the pattern is borrowed from). Default-implemented on
`IHypergraphQuery` unless noted.

### 5.1 s-connected-components (Kernel) — MUST

```csharp
namespace Topos.Hypergraph;

public partial interface IHypergraphQuery
{
    /// <summary>
    /// Partitions vertices into s-connected components: two vertices are in the same component if
    /// an s-walk connects them (a sequence of vertices pairwise sharing ≥ <paramref name="s"/>
    /// hyperedges). <paramref name="s"/>=1 reduces to <see cref="GetConnectedComponents"/>.
    /// GDS cannot verify this (no binary projection); the HNX oracle
    /// (<c>tests/Topos.Tests.HnxOracle</c>) verifies against HNX's <c>s_connected_components</c>.
    /// Built on the existing <see cref="SWalk"/> adjacency — no new storage.
    /// </summary>
    IReadOnlyList<IReadOnlyList<Handle>> GetSConnectedComponents(int s = 1);
}
```

- **Implementation:** derive the s-adjacency (two vertices adjacent iff they share ≥ s hyperedges —
  reuse `SWalk`'s existing pairwise-incidence logic), then standard union-find or BFS over that
  derived graph.
- **Complexity:** O(V² · E) naive (pairwise share-count); document this honestly and gate a
  faster path behind a measured workload (M0 benchmark-gate discipline).
- **Oracle:** HNX `s_connected_components(s=)`. `[verified:src=pnnl/HyperNetX/classes/hypergraph.py]`
- **Tests:** `SConnectedComponentsTests` — small fixed hypergraphs with hand-computed components
  at s=1,2,3; HNX-parity tests for s≥1. **Skip gracefully if HNX unreachable.**
- **Provenance:** HNX + SimpleHypergraphs.jl both implement this; the definition is Aksoy et al.
  2020. `[verified:web=doi.org/10.1140/epjds/s13688-020-00231-0]`

### 5.2 s-line-graph (Kernel) — MUST

```csharp
public partial interface IHypergraphQuery
{
    /// <summary>
    /// The s-line-graph of this hypergraph: a binary graph whose nodes are this hypergraph's
    /// hyperedges, with an edge between two hyperedges iff they share ≥ <paramref name="s"/> common
    /// member vertices. <paramref name="s"/>=1 is the standard line graph. This is the substrate
    /// s-distance/s-centrality reduce onto. Returns the line graph as an adjacency structure
    /// (handle → neighboring hyperedge handles).
    /// HNX + xgi cross-check oracle.
    /// </summary>
    IReadOnlyDictionary<Handle, IReadOnlyList<Handle>> GetSLineGraph(int s = 1);
}
```

- **Implementation:** for each pair of hyperedges, count shared members; link if ≥ s. O(E² · avg-members).
- **Oracle:** HNX `get_linegraph(s=)` **and** xgi `to_line_graph(H, s=1)` — two independent
  implementations of the same cited definition; agreement is a strong signal.
  `[verified:src=pnnl/HyperNetX/classes/hypergraph.py, xgi-org/xgi/xgi/convert/line_graph.py]`
- **Tests:** `SLineGraphTests` — hand-computed line graphs at s=1,2; HNX-parity + xgi-parity.
- **Provenance:** Aksoy et al. 2020; independently implemented by xgi (Landry/Young et al.).

### 5.3 s-diameter / s-eccentricity (Kernel) — MUST

```csharp
public partial interface IHypergraphQuery
{
    /// <summary>
    /// The s-diameter: the maximum s-distance over all reachable vertex pairs (null if the graph
    /// has no s-connected pair). One "s-distance" hop crosses a hyperedge, restricted to the
    /// s-adjacency. Built on <see cref="SWalk.Distance"/> generalized to s≥1.
    /// HNX oracle (<c>diameter</c>/<c>edge_diameter</c>).
    /// </summary>
    int? GetSDiameter(int s = 1);
}
```

- **Implementation:** pairwise `SWalk.Distance` max, or BFS from every vertex over the s-adjacency.
- **Oracle:** HNX `diameter(s=)` / `edge_diameter(s=)`.
- **Tests:** `SDiameterTests` — hand-computed + HNX-parity.
- **Provenance:** HNX; SimpleHypergraphs.jl `diameter` as independent cross-check (its
  Dijkstra-on-s-adjacency decomposition is algorithmically transparent).

### 5.4 Directed SCC (Knowledge package, layer-1) — MUST

```csharp
namespace Topos.Hypergraph.Knowledge;

public static partial class DirectedTraversal
{
    /// <summary>
    /// Strongly-connected components over the role-aware directed graph: two vertices are
    /// strongly connected if each is reachable from the other following only
    /// <paramref name="fromRole"/>→<paramref name="toRole"/> hyperedge legs. Tarjan's
    /// single-DFS algorithm (iterative, to avoid stack-depth limits — same discipline as the
    /// kernel's <c>GetDfs</c>).
    ///
    /// This is the directed counterpart to the kernel's <see cref="IHypergraphQuery.GetConnectedComponents"/>
    /// (which is WCC-equivalent). The kernel deliberately doesn't offer directed SCC because
    /// direction needs role-awareness — a layer-1 concern (spec §4.1).
    /// GDS-verifiable via <c>gds.scc</c> over a role-projected directed graph.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<Handle>> DirectedScc(
        this IHypergraphQuery graph, byte fromRole, byte toRole);

    /// <summary>DirectedScc, typed-role overload — see <see cref="docs/ROLE_CONVENTIONS.md"/>.</summary>
    public static IReadOnlyList<IReadOnlyList<Handle>> DirectedScc<TRole>(
        this IHypergraphQuery graph, TRole fromRole, TRole toRole);
}
```

- **Implementation:** Tarjan's SCC (iterative), walking only `fromRole`→`toRole` legs — same
  role-filtering pattern as the existing `DirectedBfs`/`DirectedShortestPath`.
  `[verified:src=src/Topos.Hypergraph.Knowledge/DirectedTraversal.cs]`
- **Oracle:** GDS `gds.scc` — the standard SCC algorithm, applied to the role-projected directed
  graph (project Anchor→Target legs as directed binary edges, run `gds.scc`, compare).
- **Tests:** `DirectedSccTests` — hand-computed SCCs on small role-tagged hypergraphs; GDS-parity.
- **Provenance:** yamafaktory/hypergraph implements SCC (Kosaraju's) as a trait default
  (`strongly_connected_components`). `[verified:src=yamafaktory/hypergraph/src/core/query/structural.rs]`
- **Why must-have, not good-to-have:** NexusVerifier's AND-OR proof chainer needed exactly this,
  read the kernel's `HasCycle` doc, correctly backed off, and hand-rolled a per-DFS-path guard
  instead (`docs/NEXUS_VERIFIER_INTEGRATION_FINDINGS.md` finding #4). Two real consumers needing
  the same directed-cyclicity check is the M9-style forcing evidence.

### 5.5 Centrality: degree / closeness / betweenness (Kernel) — MUST

```csharp
namespace Topos.Hypergraph;

/// <summary>Standard centrality measures over the topology-only adjacency. GDS-verified.</summary>
public static class Centrality
{
    /// <summary>Per-vertex degree = number of hyperedges the vertex is a member of.</summary>
    public static IReadOnlyDictionary<Handle, int> Degree(IHypergraphQuery graph);

    /// <summary>
    /// Closeness centrality: reciprocal of the sum of shortest-path distances to all reachable
    /// vertices. Built on <see cref="IHypergraphQuery.GetShortestPathLength"/>.
    /// </summary>
    public static IReadOnlyDictionary<Handle, double> Closeness(IHypergraphQuery graph);

    /// <summary>
    /// Betweenness centrality: how often a vertex lies on a shortest path between two others.
    /// Brandes' algorithm over the topology-only adjacency.
    /// </summary>
    public static IReadOnlyDictionary<Handle, double> Betweenness(IHypergraphQuery graph);
}
```

- **Oracle:** GDS `gds.degree` / closeness / betweenness — direct oracles.
- **Tests:** `CentralityTests` — hand-computed on small graphs + GDS-parity.
- **Provenance:** yamafaktory/hypergraph `compute_centrality` (degree+closeness+betweenness →
  `CentralityScores`). `[verified:src=yamafaktory/hypergraph/src/core/query/projections.rs]`

### 5.6 PageRank (Kernel) — MUST

```csharp
namespace Topos.Hypergraph;

public static partial class PageRank // or a new static class
{
    /// <summary>
    /// PageRank over the topology-only adjacency (co-membership = undirected link, or use the
    /// reified-edge-as-link direction). Power iteration to convergence within
    /// <paramref name="tolerance"/> or <paramref name="maxIterations"/>. Returns per-vertex rank.
    /// GDS-verified (<c>gds.pageRank</c>).
    /// </summary>
    public static IReadOnlyDictionary<Handle, double> Compute(
        IHypergraphQuery graph, double damping = 0.85, int maxIterations = 100, double tolerance = 1e-6);
}
```

- **Oracle:** GDS `gds.pageRank` — direct oracle.
- **Tests:** `PageRankTests` — hand-computed on a tiny graph (convergence sanity) + GDS-parity on
  larger graphs.
- **Provenance:** yamafaktory/hypergraph `compute_page_rank` (power iteration).
  `[verified:src=yamafaktory/hypergraph/src/core/query/projections.rs]`
- **Open fork (Nasser):** which adjacency — symmetric co-membership (topology-only, matches the
  rest of the kernel) or the stored Incidence direction (role-aware, would belong in Knowledge)?
  Recommend **symmetric, kernel-level** for v1 (matches `GetBfs` etc.); a directed PageRank variant
  can follow in Knowledge if a consumer needs it. See §6.3.

### 5.7 Hypergraph modularity (Kernel) — GOOD-to-have (oracle-fragmented)

```csharp
namespace Topos.Hypergraph;

public static partial class Modularity // extends existing Modularity.cs
{
    /// <summary>
    /// Hypergraph modularity per the <b>Kumar et al. (2020)</b> definition (the formulation HNX
    /// ships). Measures community quality for a given vertex partition, accounting for n-ary edge
    /// purity rather than clique-expanding. <b>This is one specific definition</b> — see the spec's
    /// §6.2 caveat: hypergraph modularity has 3+ competing peer-reviewed formulations; Topos picks
    /// one, cites it, and cross-checks against HNX's <c>hmod.modularity</c> only (not the others).
    /// </summary>
    public static double ComputeHypergraph(
        IHypergraphQuery graph, IReadOnlyDictionary<Handle, int> communities,
        EdgeWeighting weighting = EdgeWeighting.Linear);
}

public enum EdgeWeighting { Linear, Majority, Strict }
```

- **Implementation:** Kumar et al.'s strict/majority/linear purity weighting over n-ary edges.
  `[verified:src=pnnl/HyperNetX/algorithms/metrics/hmod.py]`
- **Oracle:** HNX `hmod.modularity` (Kumar formulation only). **Do not** cross-check against
  Chodrow-Veldt-Benson or Kamiński h-Louvain — different objective functions, will disagree.
- **Tests:** `HypergraphModularityTests` — hand-computed on a tiny partitioned hypergraph +
  HNX(Kumar)-parity. Document the definition choice loudly.
- **Provenance:** Kumar et al. 2020, as implemented in HNX.

### 5.8 HIF interchange (new package or Kernel I/O) — GOOD-to-have

- **What:** read/write the Hypergraph Interchange Format (HIF), a JSON schema both XGI and HNX
  support. `[verified:src=xgi-org/xgi/xgi/readwrite/hif.py, pnnl/HyperNetX/hypernetx/hif.py]`
- **API:** `HypergraphKernelExtensions.LoadHif(stream)` / `SaveHif(stream)`, or a standalone
  `Topos.Hypergraph.Hif` package.
- **Oracle:** round-trip parity — export a Topos graph to HIF, read it into xgi/HNX, re-export,
  compare; and read an xgi/HNX-produced HIF into Topos.
- **Why good-to-have, not must:** no forcing consumer yet; the value is future cross-tool
  portability and the M8-deferred "HIF interchange" gated item.

### 5.9 Local clustering coefficient (Kernel) — GOOD-to-have

- **What:** per-vertex LCC (fraction of neighbor pairs that are themselves adjacent). Topos has
  global `TriangleCount` but not the per-vertex coefficient.
- **Oracle:** GDS `gds.localClusteringCoefficient`.
- **Provenance:** standard; xgi and JGraphT both implement.

---

## 6. Open forks for Nasser (🟡 — your calls gate scope)

### 6.1 HNX oracle: subprocess or Docker sidecar?

The GDS oracle uses a disposable Docker container. HNX is lighter (pure Python, no server). Two
options for `tests/Topos.Tests.HnxOracle`:

- **(a) Docker sidecar `topos-hnx-oracle`** — mirrors the GDS pattern exactly; strongest isolation;
  requires Docker locally. **Recommended for consistency.**
- **(b) Python subprocess invoked per-test-run** — lighter, no Docker, but couples the .NET test
  host to a local Python install + `pip install hypernetx`. Less portable across machines.

**Recommend (a)** for parity with the GDS-oracle discipline and the documented isolation concerns
(`docs/GDS_ORACLE_SETUP.md`).

### 6.2 Which modularity definition? (only if §5.7 is in scope)

Three peer-reviewed, mutually-inconsistent definitions:

1. **Kumar et al. 2020** (linear/majority/strict purity) — what HNX ships. Simplest, has an oracle.
2. **Chodrow-Veldt-Benson** (AON/differentiated objective) — more rigorous, research-code-only
   oracle (`PhilChodrow/HypergraphModularity`, stale since 2022).
3. **Kamiński h-Louvain** (scalable) — newest, `nveldt/HyperModularity.jl` (Julia, stale).

**Recommend Kumar (option 1)** for v1 — it's the only one with a maintained, tested oracle (HNX).
Document the choice loudly. Options 2/3 can be added later as alternative formulations without
changing option 1's API.

### 6.3 PageRank: symmetric or directed adjacency?

See §5.6. **Recommend symmetric (kernel-level) for v1**; directed PageRank in Knowledge only if a
consumer asks. This keeps the kernel's "role-blind, symmetric" discipline intact.

### 6.4 Milestone designation: M11, or fold into existing milestones?

- **Option A — new milestone "M11: algorithm completeness":** cleanest narrative; one exit
  criterion (all MUST items GDS/HNX-verified, RLB + NexusVerifier consume the new APIs).
- **Option B — fold into M6 (analytics) and M9 (Knowledge):** M6 was "analytics" and arguably
  centrality/PageRank belong there; Directed SCC arguably belongs in M9. But both are shipped/closed
  milestones, so reopening them muddies the decision log.

**Recommend Option A (new M11)** — keeps closed milestones closed, gives Opus 5 one clean target.

---

## 7. Exit criterion for M11

Mirrors M9's falsifiable-gate discipline: **two real consumers exercise the new surface.**

1. **All MUST items implemented and GDS- or HNX-verified** (parity tests pass when oracles
   reachable; skip-graceful when not).
2. **RLB's `ToposGraphProjection`** gains a use of at least one new API (e.g. PageRank for
   landmark importance, or Directed SCC for transition-cycle detection) — proves the API is real,
   not theoretical. RLB's 346-test suite passes unchanged.
3. **NexusVerifier's** hand-rolled per-DFS-path cycle guard (finding #4) is replaced by
   `DirectedScc` — the M9-forcing-evidence pattern repeated: a real consumer drops its parallel
   copy in favor of Topos's generic implementation.
4. **177 → N test count grows** with the new algorithm tests; the full Topos test suite passes.
5. **HIF (if scoped)** round-trips against xgi/HNX.

---

## 8. What this spec does NOT propose (scope boundaries)

- **No storage-contract changes.** Handle/Vertex/Incidence/PropertyKey untouched; both invariants
  intact. Every algorithm here is a default method on `IHypergraphQuery` or a Knowledge extension.
- **No M7 spectral eigensolver.** Still deferred; this spec adds Laplacian-adjacent work only if
  modularity (§5.7) needs it, and even then as a builder, not a general eigensolver.
- **No neural/embedding models.** Topos stays a store; DHG owns the learning-framework niche.
- **No concurrency-model changes.** The `ReaderWriterLockSlim`-per-pool model (M0 benchmark-corrected)
  stands; new algorithms are read-only over `IHypergraphQuery`.

---

## 9. Sources

- Full survey: `docs/ALGORITHM_SURVEY.md` (fresh independent multi-language pass, 2026-07-30).
- s-walk framework: Aksoy et al., "Hypernetwork Science via High-Order Hypergraph Walks," EPJ Data
  Science 9:16 (2020), `[verified:web=doi.org/10.1140/epjds/s13688-020-00231-0]`.
- HNX (oracle): `pnnl/HyperNetX` v2.4.3, BSD-3-Clause.
- xgi (cross-check): `xgi-org/xgi` v0.10.2.
- yamafaktory/hypergraph (algorithm provenance): v4.2.0, MIT — `HypergraphQuery` trait, 9
  primitives → ~40 algorithms including centrality/PageRank/SCC.
  `[verified:src=src/core/query/trait_def.rs, projections.rs, structural.rs]`
- Existing Topos architecture: `docs/SPECIFICATION.md §4.1` (layer split), `§5` (GDS oracle),
  `§6` (milestone roadmap); `docs/DECISIONS.md` (locked vs. open).
- Existing Topos algorithm surface: `src/Topos.Hypergraph/IHypergraphQuery.cs`,
  `SWalk.cs`, `LabelPropagation.cs`, `TriangleCount.cs`, `Modularity.cs`;
  `src/Topos.Hypergraph.Knowledge/DirectedTraversal.cs`.
