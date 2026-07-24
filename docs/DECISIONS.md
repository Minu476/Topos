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

### 2026-07-23 — CLAUDE REVIEW OF SPECIFICATION: 4 resolved, 3 opened

- **Decider:** GLM-5.2 (applying Claude's review), pending Nasser confirmation on the opened items
- **Question:** Claude's first review of `docs/SPECIFICATION.md` surfaced 7 issues beyond the
  doc's own §12 open questions (saved verbatim at `docs/reactions/03_Claude_specification-review.md`).
- **Decisions:**
  - **#1 RESOLVED** — §1.2's empirical case was over-anchored on the `r[3]=1.0` discriminator
    (a patchable implementation quirk, not a theorem). Re-anchored on the **Condition-aggregation
    semantics**: joint eligibility over N members produces one atomic event with one reward
    signal and joint statistics — binary can represent the topology but not the atomic event
    semantics (credit assignment, joint SuccessRate, hard-gate conjunction under learning).
    Softened "proof" to "pragmatic non-derivability, evidenced in production code." The `r[3]`
    flag is now corroborating code evidence, not the load-bearing argument.
  - **#3 RESOLVED** — added §3.4 concurrency model (🔒 LOCKED): SWMR at the kernel boundary +
    lock-free `Interlocked.Increment` counters + per-pool write locks + copy-on-write pages for
    snapshot readers. Derived from yamafaktory's `AtomicU64` pattern + RLB's existing
    `ConcurrentDictionary` storage + EnTT's per-pool design.
  - **#5 RESOLVED** — cardinality/validation rules (e.g., RLB's D2) live in **layer 1
    (Knowledge model)**, not the kernel. Resolves the apparent tension with "the store records,
    it doesn't judge" (§3.2 Q4): the contract has no cap; the layer-1 domain type enforces it at
    construction. Clarifies the RLB-port seam (Q5): `HyperEdge.ValidateCardinality` becomes a
    layer-1 domain type.
  - **#7 RESOLVED** — added one-sentence scope clarification (§2.2): Topos stores
    `fact`/`belief`/mode-flag but does *not* reason over them (no contradiction resolution,
    truth-maintenance, or entailment). Storing ≠ revising.
- **Opened (pending Nasser/reviewer input):**
  - **#2 → Q7** — Handle generation-bits: include from M0 (lean) or add at M4? Resolution
    stated (logical persistence ≠ physical slot relocation under M4 compaction), but the
    storage-layout call is Nasser's.
  - **#4 → Q8** — derive the per-hop latency budget from the 270Hz step figure. M0 gate now
    requires an absolute number; the exact derivation needs the step-budget split confirmed.
  - **#6 → Q9** — verify GDS per-algorithm Community/Enterprise tier. GPLv3 licensing handled
    by test-only isolation (🔒 LOCKED in §5.1); the algorithm-tier question is owed verification
    before M6.
- **Rationale:** Claude's triage was correct — #1, #3, #5 were the pre-contract-lock items (the
  empirical opener + two missing decisions). All three are resolved; §3 is now defensible as
  locked modulo Q7 (Handle layout). The three opened items are genuine adjudication questions,
  not gaps the doc should have caught.
- **What it changes:** `SPECIFICATION.md` §1.2 (re-anchored), §2.2 (scope clarification),
  §3.4 (new concurrency subsection), §3.5 (new Handle-generation subsection), §4 (cardinality
  placement stated), §5.1 (new GDS-licensing subsection), §6 M0 (dual benchmark gate), §11/§12
  (tables updated with 4 new locked + 3 new open items). `reactions/03_Claude_specification-review.md`
  preserved verbatim.

### 2026-07-23 — GPT REVIEW OF SPECIFICATION: approved for implementation; 5 changes applied

- **Decider:** GPT (approved), GLM-5.2 (applied), pending Nasser sign-off on opened Q10
- **Question:** GPT's review of `docs/SPECIFICATION.md` (saved verbatim at
  `docs/reactions/04_GPT_specification-review.md`) — approved for implementation with 5
  requested changes. Scores: technical vision 9.5/10, engineering maturity 9/10, evidence
  discipline 9.5/10, specification clarity 8.5/10, risk of overengineering 7/10.
- **Decisions (all 5 applied):**
  - **#1 APPLIED** — §1 compressed ~40%. Now leads with a 3-line thesis ("RLB learns over
    atomic n-ary events; binary decomposition destroys the atomicity of learning; therefore
    the storage substrate should preserve n-ary structure"). Reframed explicitly as a workload
    argument, not a mathematical-impossibility claim.
  - **#2 APPLIED** — reviewer-attribution removed from the spec body. ~20 inline "per Claude
    review #X" / "GPT's reframe" / "Fable's" parentheticals converted to neutral "Design note"
    framing or removed. Process provenance consolidated in a new Appendix A (Design History);
    evidence provenance (`[verified:src]` tags) unchanged in the body.
  - **#3 APPLIED** — "AI age" / "AI-memory niche" reworded to workload descriptors
    ("long-lived adaptive symbolic memory workloads"). Body is now era-neutral.
  - **#4 APPLIED** — §4 architecture revised: the top "Algorithms" layer is no longer a single
    stratum. The spec now has a **3-layer substrate** (Knowledge / Graph / Storage, 🔒 LOCKED)
    plus **5 cross-cutting capabilities** above it (Traversal / Analytics / Learning / AI
    services / Projection). Rationale: different algorithm families operate on different
    layers — a shortest-path routine has nothing in common with a theta-update except "runs on
    graphs." Note: this revises GPT's *own* earlier 5-layer proposal; the substrate is
    unchanged. Opened Q10 on the exact capability partition.
  - **#5 confirmed** — evidence discipline maintained.
- **Two framing points acknowledged (no contract change):**
  - **"Memory Event" abstraction** — GPT's largest architectural comment: the deeper
    abstraction is *memory*, not graphs/hypergraphs/incidences; hypergraph may be one
    implementation of a "persistent symbolic memory substrate." Added as explicit **horizon**
    in new §2.4, NOT a contract restructure — per GPT's own caveat ("don't change the
    architecture today... earn the right to generalize"). The M5 second-consumer test is the
    checkpoint where generalization becomes a live question.
  - **Identity question** — GPT endorses the current lean (build the hypergraph M0–M3, hold
    the incidence-model reframe). Confirmed in §2.3–§2.4.
- **Rationale:** GPT's feedback was overwhelmingly editorial (the doc "crossed an important
  threshold... reads like the design specification for a serious systems project"). The one
  substantive architectural change (#4, algorithms-as-capabilities) revises GPT's own earlier
  proposal and is genuinely cleaner — algorithms do cut across layers. The "Memory" horizon is
  kept as framing, not structure, exactly as GPT recommended.
- **What it changes:** `SPECIFICATION.md` §1 (compressed, thesis-led), §2.1 (workload
  rewording), §2.4 (new horizon subsection), §3.4/§3.5/§5.1 (neutral design notes), §4 (fully
  revised: 3-layer substrate + capabilities), §11 (table updated), §12 (Q10 added), Appendix A
  (new). `reactions/04_GPT_specification-review.md` preserved verbatim.

### 2026-07-23 — GPT FINAL APPROVAL OF SPECIFICATION: no further changes requested

- **Decider:** GPT — approved
- **Question:** GPT's re-review of `SPECIFICATION.md` after the 5 changes from
  `04_GPT_specification-review.md` were applied. (Saved verbatim at
  `docs/reactions/05_GPT_specification-final-approval.md`.)
- **Decision:** **APPROVED for implementation.** Scores: problem definition 10/10, evidence
  discipline 9.5/10, architectural consistency 9.5/10, scope control 9.5/10, roadmap realism
  9/10, technical credibility 9.5/10. "This now reads much more like the specification for a
  serious systems project than a conceptual proposal."
- **No further changes requested.** Two observations, both non-blocking:
  1. **Philosophical identity risk** (hypergraph library vs universal symbolic-AI substrate)
     — GPT confirms the current lean (build hypergraph M0–M3) "is exactly right": "If, three
     years from now, people discover that Topos naturally projects to RDF, property graphs,
     relational tables, and hypergraphs… then the market will rename it for you." Maps to
     existing Q2; no action. `[confirmed — §2.3–§2.4 already capture this lean + horizon]`
  2. **Tiny suggestion: trim 10–15% repetition** (topology-vs-atomicity, RLB-first rationale
     appear in multiple sections). GPT's own framing: "isn't wrong—it reinforces the thesis…
     after a few more review cycles." **Deferred, not applied now** — the spec is approved,
     and further doc-polishing is the overengineering both reviewers warned against (7/10
     risk). Trimming happens naturally during M0 as code clarifies what's load-bearing, and as
     a final polish pass before OSS (M8). `[deferred to M0/M8]`
- **What it changes:** nothing in the spec. **The specification phase is complete.** Both
  reviewers (GPT, Claude) have approved. The remaining open questions (Q1–Q3, Q7–Q10) are
  engineering adjudication items, not design-coherence questions — exactly the kind a spec
  should "leave for implementation to answer."

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
