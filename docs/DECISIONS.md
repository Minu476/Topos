# Decisions — what the reviewers locked, what they left open, what's Nasser's call

**Date:** 2026-07-23
**Status:** Living document. Updated as decisions are made.

This captures the synthesis of two external reviews (GPT, Fable) on
`BASE_INVESTIGATION.md`. It is the authoritative reference for what is *settled* going into
the spec, what is *open*, and what requires Nasser's explicit adjudication. Read this before
either re-reading the reactions or writing the spec.

The full reviews are verbatim in `reactions/01_GPT_first-reaction.md` and
`reactions/02_Fable_first-reaction.md`.

---

## 1. Settled — both reviewers agree, adopt without further debate

These five answers came back from both reviewers with no daylight between them. The spec can
treat them as decided.

| # | Question | Decision | Rationale (both reviewers) |
|---|---|---|---|
| 1 | **Reserved hot-path slots for `Roles` and `Status`?** | **YES — reserved struct fields on the Vertex record.** | Both are read on every traversal hop (skip-dormant, role-gated expansion); a PropertyBag indirection on the innermost loop is the wrong tier. EnTT's evidence supports it. Rule: *reserved slot only if read per-hop by core traversal* — this isn't a slippery slope. |
| 2 | **Spectral machinery (Laplacian, KaHyPar partitioning)?** | **STAY DEFERRED.** M7 stays at the end of the roadmap. | Nothing in the current or plausible-next workload pulls it earlier. If graph-embedding retrieval becomes real, it'll arrive with its own requirements. Three voices now agree (the investigation + both reviewers). |
| 3 | **Packaging — one assembly from day one, or split?** | **ONE assembly through M3; split at the persistence boundary at M4** (`.Core` + `.Persistence`). | Splitting from day one imposes API-stability tax during M0–M2 — exactly the phase where primitive shapes most need freedom to change. The persistence boundary is the one seam that's genuinely stable by then. |
| 4 | **Reification depth cap in the storage contract?** | **NO CAP in the contract.** | A depth cap is a judgment; the store records, it doesn't judge. Bounding traversal belongs in the expansion policy at query time — where it can differ per use (explanation queries: depth 2; provenance audits: unbounded). |
| 5 | **Embeddings — first-class `Vertex.Embedding` field or `PropertyKey<T>`?** | **`PropertyKey<float[]>` with the ANN index as a separate derived structure keyed by Handle.** NOT a first-class field. | Keeps the kernel symbolically pure; makes the ANN index rebuildable (computed judgment over stored records). Matches how Kuzu did it (extension, not core). **Note: float, not double** — embedding models emit float32, and doubling memory for precision the models don't have is pure waste. |

### Plus two investigation-internal fixes both reviewers would endorse (already applied)

- **`double[]` → `float[]`** for embedding `PropertyKey<T>`. (Applied to §7 Q6 and §5.5 #19 of BASE_INVESTIGATION.md.)
- **M0 exit criterion must include *measured* benchmarks** of CSR/sparse-set against a naive
  `Dictionary<Handle, List<Handle>>` baseline on the actual workload shape. Runtime-over-inference
  discipline applied to the library itself. (Applied to §6 M0 of BASE_INVESTIGATION.md.)

---

## 2. Divergent — reviewers disagree; needs Nasser's input

### 2.1 M5 sequencing — primitive shapes early vs. all-deferred

| | Position | Rationale |
|---|---|---|
| **GPT** | Defer *all* of embeddings/learnable/provenance to M5. | Keep the kernel lean; YAGNI until a consumer forces them. |
| **Fable** | **SPLIT IT.** Primitive *shapes* in M0/M2; *machinery* in M5. | The Incidence primitive's fields are *already* justified by these concerns (cell properties, `IncidenceRole`, the mode flag lands in M2 anyway). Embeddings as `PropertyKey<float[]>` from day one costs nothing and forecloses nothing. |

**Lean: Fable.** It's more precise and consistent with the four-primitive contract — the
contract already embeds the shapes; making that explicit is honest. The investigation's §5
already mostly does this; the spec should formalize it.

**Decision needed from Nasser:** confirm "split it — shapes early, machinery in M5."

---

## 3. Strategic fork — Nasser's call to adjudicate, blocking the spec

This is the deepest disagreement, and it cannot be resolved by me or the reviewers — it's
Nasser's project strategy.

### 3.1 The fork: decouple from RLB, or build Topos as RLB's kernel first?

| | Position | Argument |
|---|---|---|
| **Nasser (current decision)** | **Decouple.** Topos is a standalone repo with no reference to RLB. RLB stays untouched until Topos is in beta. | Decoupling enforces domain-agnosticity *by construction*. Keeps RLB's 337 tests safe. Clean dependency direction (both RLB and FSDE can depend on Topos, neither on each other). |
| **Fable's challenge** | **Build Topos as RLB's kernel first**, with standalone-library ambition as a *falsifiable milestone* (the second-consumer test at M5), not as the founding assumption. | The single-consumer trap: a "domain-agnostic" library validated against exactly one consumer (RLB/FSDE) will silently take that consumer's shape. Pre-commit to a *second* consumer at M5 (even a toy chat-agent memory demo). If the kernel can't serve a consumer you didn't design it around, the "standalone library" claim isn't yet true — better to learn that at M5 than M8. |

**Both arguments are real. This is not a question with an obviously right answer.**

What Nasser's decision changes:
- **If decouple (current):** Topos stays its own repo, builds the kernel without an in-tree
  consumer, validates against synthetic benchmarks + the M5 second-consumer test (which becomes
  a *self-contained* demo, not RLB).
- **If build-as-RLB-kernel (Fable):** Topos becomes an RLB ProjectReference immediately; RLB's
  337 tests become the first real consumer; the M5 second-consumer test is a *non-RLB* demo
  (which becomes the real test of domain-agnosticity).

**Decision required before spec finalization.** The contract and roadmap are mostly the same
either way; what changes is the *validation strategy* and whether RLB gets touched during M0–M4.

---

## 4. Deeper architectural forks (GPT) — open for spec, not silently adopted

GPT proposed two reframes that, if taken, reshape the project. Neither should be folded into
the spec silently — they're forks, not corrections.

### 4.1 "Typed incidence model, not hypergraph"

> If the kernel can project to hypergraph / property-graph / RDF / relational, then perhaps
> it's not "a hypergraph library" — it's a *typed incidence model* with hypergraph as one
> projection.

- **What it changes:** the namespace (`Topos.Hypergraph` → maybe just `Topos`), the README's
  self-description, the project's identity. The name *Topos* survives either way.
- **The argument for:** it's a bigger, more defensible ambition, and it's what the four
  primitives actually describe (an incidence model — hypergraph is one view).
- **The argument against:** scope creep at the founding moment. The investigation was scoped
  to hypergraphs; leaping to "multi-projection incidence model" before even M0 is a much
  larger claim with less evidence.
- **Lean:** *hold as a reach goal*, not a founding decision. Build M0–M3 as a hypergraph
  library; the projection layer (if it materializes) becomes an M-later milestone. The
  primitive shape doesn't preclude either direction; don't lock the *identity* before the
  *implementation* exists.

### 4.2 The 5-layer architecture

GPT's proposed layers: **Knowledge model / Graph model / Storage model / AI services / Algorithms.**

- The investigation's structure already implies most of this (Storage model = §5.2; Graph
  model = §5.1; AI services = §5.5; Algorithms = §5.4). What's missing is the *Knowledge
  model* layer (typed domain concepts above the graph model) and the explicit separation of
  AI-services from Algorithms.
- **Lean:** adopt the layering explicitly in the spec. It composes cleanly with Fable's
  "split M5" answer (primitive shapes = Graph model; machinery = AI services layer).

---

## 5. Next-document queue (what to write next)

In priority order:

1. **`docs/AGENT_MEMORY_COMPETITORS.md`** — the missing survey (Zep/Graphiti, mem0, Letta,
   Cognee). Source-verified, same integrity standard as BASE_INVESTIGATION. **This is the
   investigation's biggest gap (§8.3).** Without it, the central feasibility question —
   "is the hypergraph gap unfilled because nobody built it, or because the field chose
   binary?" — is unanswered.
2. **Nasser's adjudication of §3.1** (decouple vs. build-as-RLB-kernel). This blocks spec
   finalization.
3. **The final spec** (`docs/SPECIFICATION.md`) — incorporating the contract, the 5-layer
   architecture, the resolved open questions, the roadmap, and the empirical
   paradox-compression argument from RLB as §1 (per Fable — it's the part a skeptic attacks,
   and no library survey answers it).

---

## 6. Decision log

### 2026-07-23 — STRATEGIC FORK RESOLVED: build Topos as RLB's kernel first

- **Decider:** Nasser
- **Question:** Decouple Topos from RLB and leave RLB untouched until beta, OR build Topos as
  RLB's kernel first with standalone-library ambition as a falsifiable M5 milestone?
  (Originally §3.1 of this document.)
- **Decision:** **Build as RLB's kernel first.** (Fable's recommendation, adopted.)
- **Rationale:** The single-consumer trap is the real feasibility risk. Validating Topos
  against exactly one consumer (RLB/FSDE) silently shapes the kernel around that consumer.
  Fable's mitigation: pre-commit to a *second* consumer at M5 that is NOT RLB (e.g. a minimal
  chat-agent memory demo) — if the kernel can't serve a consumer it wasn't designed around,
  the "standalone library" claim isn't yet true, and better to learn at M5 than M8.
- **What it changes:**
  - **`Rich-Learning-Base` is now in scope.** Topos becomes a `ProjectReference` in
    RLB's V2 csproj during M0–M4. RLB's 337-test suite becomes the first real consumer.
  - The M5 exit criterion becomes: a *non-RLB* second consumer (toy chat-agent memory demo)
    runs on Topos. This is the falsifiable test of domain-agnosticity.
  - The decoupling still holds at the *dependency* level: Topos still references nothing
    upstream (no RLB types leak into Topos); RLB references Topos. Clean direction preserved.
  - The name and the standalone-library ambition are unchanged. Topos is still its own repo
    and its own eventual NuGet; it's just *validated* against RLB first, not kept isolated.

### 2026-07-23 — VERIFICATION STRATEGY: Neo4j GDS as the correctness oracle

- **Decider:** Nasser
- **Question:** How do we verify Topos's algorithms (BFS/DFS/shortest-path/cycle/SCC/
  community-detection/provenance-reachability) are correct during M1+?
- **Decision:** **Use Neo4j GDS (Graph Data Science library) as an independent oracle.**
  For each algorithm Topos implements, run the same query against the same graph in Neo4j
  GDS and assert the outputs match. This is the Lean-for-NexusVerifier pattern applied to
  graph algorithms: a trusted external implementation as ground truth.
- **Rationale:** Neo4j GDS is mature, battle-tested, and independent of Topos's
  implementation. Property-graph projection of a hypergraph (via the ProjectionEngine from
  BASE_INVESTIGATION §5.4) gives a binary-graph view GDS can operate on; for algorithms where
  the hypergraph and its projection disagree (s-walk, role-gated reachability), Topos's
  answer is the novel claim and GDS can't verify it — but for the standard algorithms
  (shortest path, SCC, cycle, PageRank, community detection via Louvain), GDS is the oracle.
- **What it changes:**
  - **M1's exit criterion adds a GDS-parity test suite.** Every algorithm M1 ships must
    pass a property-based test: generate random graphs, run Topos + GDS, assert equal
    results (modulo ordering).
  - M6 (analytics — community detection, modularity) gets a strong verification path: GDS
    ships Louvain, Label Propagation, Weakly/Strongly Connected Components, Triangle
    Counting, Local Clustering Coefficient — all direct oracles for Topos's analytics.
  - Neo4j is already in Nasser's stack (RLB uses `Neo4jGraphMemory`, FSDE uses Neo4j). No
    new infrastructure. The GDS plugin install is the only addition.
  - Test harness: a small `Topos.Tests.GdsOracle` project (or test class) that loads a
    graph into both Topos and Neo4j, runs paired algorithms, diffs results. Added to the
    roadmap as part of M1.

---

## 7. Decision log format for future entries

When Nasser or a reviewer makes a decision that closes an open item, append it to §6 above as:

```
### YYYY-MM-DD — <Decision title>
- **Decider:** Nasser / reviewer / consensus
- **Question:** <one line>
- **Decision:** <one line>
- **Rationale:** <2-3 lines>
- **What it changes:** <files/milestones affected>
```

Keep this document the single source of truth for what's decided. The reactions stay
frozen as the record of the debate; this document is the resolution.
