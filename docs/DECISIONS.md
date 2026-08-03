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

### 2026-07-25 — M8 API-FREEZE: NexusVerifier integration findings resolved

- **Decider:** Nasser (via AskUserQuestion during the M8 session), executed by Opus 5.
- **Question:** `docs/NEXUS_VERIFIER_INTEGRATION_FINDINGS.md` raised six findings from Topos's
  second real consumer (NexusVerifier's AND-OR proof-search chainer, July 2025). M8 is the
  API-freeze milestone; each needed an explicit decision rather than another deferral.
- **Decision:**
  1. **Finding #1 (per-Incidence cell properties documented but not built):** fix the doc, don't
     build the mechanism. `Incidence.cs` now states plainly that cell-level properties are a
     layer-1 concern (reify the membership as its own edge-vertex, or keep a side index) — no
     `SetProperty(key, incidence, value)` overload added.
  2. **Finding #2a (`Handle.Invalid`):** wire it in. `HypergraphKernel.TryGetVertex`,
     `FilteredView.TryGetVertex`, and `UnionView.TryGetVertex` now set the out `Vertex`'s `Handle`
     to `Handle.Invalid` on failure, replacing the ambiguous `default(Handle)` (Index 0). This is
     a real behavioral change to public API output on the failure path (the `bool` return value
     is unchanged); callers who correctly check the `bool` see no difference.
  3. **Finding #2b (`Generation`):** stays reserved, not wired to anything. `Handle.cs`'s doc
     corrected from the stale "reserved for M4" (M4 shipped without using it) to "reserved for a
     future compaction milestone."
  4. **Finding #3 (role registry):** documented convention only, no kernel change — see new
     `docs/ROLE_CONVENTIONS.md`. `AddIncidence` keeps taking a raw `byte`; consumers are guided
     toward a `byte`-backed `enum` cast pattern instead of ungrounded `const byte` fields.
  5. **Finding #4 (`HasCycle` misleads on n-ary graphs):** amplified in place — the "trivially
     cyclic on 3+ member hyperedges" warning moved to the front of the doc as a loud one-line
     callout, with the NexusVerifier near-miss cited as evidence the doc already does its job.
     Not renamed; renaming was considered and rejected as unnecessary churn given the doc now
     leads with the warning.
  6. **Finding #5 (role-aware traversal at the kernel layer):** confirmed as staying out of the
     kernel, not just deferred. `IHypergraphQuery`'s class doc now states this explicitly, naming
     it as the most likely first content of a future M9+ `Topos.Hypergraph.Knowledge` package.
  7. **Finding #6 (RLB's `IGraphMemory` ergonomics):** informational only, not a Topos action —
     no change made; recorded as a lesson for any future Topos interface that grows an "optional
     capability" split (apply default-implementation escape hatches to *all* members from day
     one, not retroactively).
- **Rationale:** M8 exists to convert carried-since-M0 hedges into locked conventions. Two of the
  six findings were real decisions (cell properties, `Handle.Invalid`); the rest were ergonomic
  or already-correct-by-design. Every finding came from source-level work against a real second
  consumer, not speculation — see the findings doc for full detail and citations.
- **What it changes:** `src/Topos.Hypergraph/Incidence.cs`, `Handle.cs`, `HypergraphKernel.cs`,
  `FilteredView.cs`, `UnionView.cs`, `IHypergraphQuery.cs`; new `docs/ROLE_CONVENTIONS.md`; new
  regression tests in `HypergraphKernelTests.cs` and `ViewsTests.cs` pinning the
  `Handle.Invalid`-on-failure contract. No change to `Topos.Hypergraph.Persistence` or any
  existing public method signature — the `Handle.Invalid` change is a same-signature output-value
  change on an already-failing path.

### 2026-07-25 — M8 SCOPE: HIF interchange and docs site deferred; package versions corrected

- **Decider:** Opus 5, on request from Nasser to decide what's next for M8.
- **Question:** Spec §6's M8 row lists four items: API stability, benchmark suite, HIF
  interchange support (port from Julia/HyperNetX), and a docs site, exiting on "OSS-ready." The
  NexusVerifier-findings slice (above) covered API stability. What's the sequencing for the rest?
- **Decision:**
  - **Benchmark suite: already satisfied**, no action — `benchmarks/Topos.Hypergraph.Benchmarks`
    (BenchmarkDotNet) has existed since M0 and produced the measured data in
    `docs/M0_BENCHMARK_RESULTS_2026-07-24.md`.
  - **API stability: continues** with a broader audit of the public surface beyond the six
    NexusVerifier findings (in progress as of this entry; see the next log entry once it lands).
  - **HIF interchange (port from Julia/HyperNetX): deferred, not dropped.** No consumer —
    RLB, ChatMemory, or NexusVerifier — has ever needed interchange with another hypergraph
    library's file format. Building it now would be exactly the speculative-generality this
    project's own discipline warns against elsewhere (the same reasoning that kept M7 spectral
    machinery deferred: "three voices agree... nothing since has forced it"). Re-entry condition:
    a real consumer needs to import/export hypergraph data across systems, or Nasser decides
    ecosystem interop is a deliberate positioning bet independent of any single consumer.
  - **Docs site: deferred.** A public docs site is a consequence of an OSS-release-timing
    decision (the repo is currently private — `github.com/Minu476/Topos`), not a standalone
    engineering task. Building one before that timing is decided front-runs a decision that isn't
    made yet. Re-entry condition: Nasser decides to make the repo public / publish to NuGet.
  - **Package version strings corrected.** Both `Topos.Hypergraph.csproj` and
    `Topos.Hypergraph.Persistence.csproj` still said `0.1.0-m0` / `0.1.0-m4` despite M0–M6 being
    fully shipped — bumped both to `0.1.0-m8` (same versioning convention: pre-1.0, milestone
    suffix tracks current state, not a semver promise). `Topos.Hypergraph.csproj`'s `Description`
    also still said "M0: storage kernel" — corrected to describe the actual current capability
    (four primitives, ~40 algorithms, reification, views, embeddings, learnable edges).
  - **NuGet-publish readiness (LICENSE, `PackageLicenseExpression`, `RepositoryUrl`, actually
    publishing) is explicitly NOT decided here** — license choice is Nasser's call, not an
    engineering default to guess at. Flagged, not actioned.
- **Rationale:** Same "build what's forced, defer what isn't" discipline this project has applied
  consistently since the investigation phase (M7's deferral, the reification-depth-cap decision
  in §1.4, the no-registry role-byte decision above). Two of M8's four spec items had a live
  forcing function (a third real consumer just exercised the public API); two didn't.
- **What it changes:** `src/Topos.Hypergraph/Topos.Hypergraph.csproj`,
  `src/Topos.Hypergraph.Persistence/Topos.Hypergraph.Persistence.csproj` (version + description).
  No functional code changes. HIF interchange and the docs site are not scheduled; revisit only
  under the stated re-entry conditions.

### 2026-07-25 — M8 API AUDIT: broader public-surface findings resolved

- **Decider:** Nasser (via AskUserQuestion on the two breaking-change items), executed by Opus 5.
- **Question:** A read-only audit of the rest of `src/Topos.Hypergraph`'s and
  `src/Topos.Hypergraph.Persistence`'s public surface (beyond the six NexusVerifier findings
  above) turned up two design decisions and five mechanical doc/consistency gaps. What to do
  with each?
- **Decision:**
  1. **`PropertyKey<T>`'s constructor is now `internal`, not public.** The public primary
     constructor let a caller construct two keys sharing an `Id` but backed by different `T`s
     (e.g. `new PropertyKey<int>("foo", 5)` vs. `new PropertyKey<string>("foo", 5)`), which
     `PropertyRegistry`'s own doc already flagged as throwing `InvalidCastException` on first pool
     access. A repo-wide grep confirmed only `PropertyRegistry.Resolve<T>` legitimately
     constructs one. Closed now, before any external consumer could depend on the public ctor.
  2. **`SparseSet<T>` is now `internal`, matching `PropertyPool<T>`/`IncidenceIndex`.** It was the
     one outlier storage-plumbing type left public with no documented reason. Added
     `InternalsVisibleTo` for `Topos.Hypergraph.Tests` and `Topos.Hypergraph.Benchmarks` in
     `Topos.Hypergraph.csproj` (the repo's first use of `InternalsVisibleTo`) since both reference
     it directly for white-box tests/benchmarks.
  3. **`SWalk.Reachable` now throws its `ArgumentOutOfRangeException` eagerly, not deferred to
     enumeration.** Found while documenting exception behavior, not in the original audit:
     `Reachable` was a bare `yield return` iterator with a guard clause before the first yield —
     C# doesn't run that check until the caller enumerates (`.ToList()`/`foreach`), so
     `SWalk.Reachable(graph, start, s: 0)` alone didn't throw, only consuming it did. Split into
     an eager-validating wrapper calling a private iterator core, matching `SWalk.Distance`'s
     already-eager behavior. New regression test pins the eager-throw contract without
     enumerating.
  4. **Missing XML docs filled in**, no behavior change: `IHypergraphQuery.TryGetVertex` and
     `GetVertex` (including their `Handle.Invalid`/`KeyNotFoundException` contracts, which were
     documented elsewhere but not at the primitive's own declaration), `HypergraphKernel`'s
     `CreateVertex`/`ResolveProperty`/`SetProperty`/`TryGetProperty`/`RemoveProperty`/`Reactivate`,
     and `HypergraphViews`' `Subgraph`/`Mask`/`Union`/`Intersect` (previously only `Difference`
     had a doc).
  5. **Exception behavior documented** on `LearnableEdge.Evaluate`, `VectorIndex.NearestNeighbors`,
     and `SWalk.Distance`/`Reachable` — following `PropertyRegistry`'s existing pattern of calling
     out the risk in prose rather than leaving it to the throw's own message text.
  6. **`Modularity.Compute`'s two different fallback behaviors for uncommunitied vertices**
     (excluded from the internal-edges numerator, but bucketed into a synthetic community for the
     sum-of-squares term) documented in place — behavior unchanged, now explained.
- **Rationale:** Items 1-2 are real API-surface tightening, done now while nothing external
  depends on the public shape (the same "M8 is the freeze moment" reasoning as the
  NexusVerifier-findings entry above). Item 3 is a correctness gap in guard-clause consistency,
  not a design question — fixed outright. Items 4-6 are pure documentation, matching this
  codebase's own established doc-density standard.
- **What it changes:** `PropertyKey.cs`, `SparseSet.cs`, `Topos.Hypergraph.csproj` (behavioral/
  visibility changes); `SWalk.cs` + new `SWalkTests.cs` regression test (behavioral fix);
  `IHypergraphQuery.cs`, `HypergraphKernel.cs`, `HypergraphViews.cs`, `LearnableEdge.cs`,
  `VectorIndex.cs`, `Modularity.cs` (doc-only). 177 tests pass (up from 176).

### 2026-07-25 — M8 CLOSED (API-stability scope)

- **Decider:** Nasser (via AskUserQuestion, choosing "call M8 done as-is" over making the license
  call now or holding off entirely).
- **Question:** After two API-stability passes this session (the six NexusVerifier findings, then
  the broader public-surface audit), is there more API-stability work to do, or does M8 close here?
- **Decision:** **M8's API-stability scope is done.** No further open findings — the audit fork
  explicitly confirmed the rest of the public surface (`EdgeStatistics`, `HandleAllocator`,
  `Provenance`, `LabelPropagation`, `TriangleCount`, the persistence package) already meets this
  codebase's documentation standard. The two remaining spec-listed M8 items —
  **HIF interchange** and a **docs site** — stay deferred with the re-entry conditions already
  logged above (a forcing consumer, or a decision to go public). **NuGet-publish readiness**
  (license file, `PackageLicenseExpression`, `RepositoryUrl`, actually publishing) is a distinct,
  separately-gated future task, not part of M8's closure — it waits on Nasser deciding a license
  and a public-release timing, neither decided yet.
- **Rationale:** M8's actual forcing function (a live third consumer exercising the public API)
  has been fully addressed. The remaining spec items don't have a forcing function today, and
  building them speculatively would be exactly the "build what's forced, defer what isn't"
  violation this project has avoided everywhere else (M7, the role-registry decision, the HIF/
  docs-site deferral itself). Declaring M8 closed here — rather than leaving it perpetually "in
  progress" waiting on OSS-timing decisions that aren't imminent — keeps the roadmap status
  honest.
- **What it changes:** No code. Status framing only, in `AGENTS.md` and
  `docs/SESSION_HANDOFF.md` — M8 moves from "in progress" to "done (API-stability scope);
  HIF/docs-site/NuGet-publish remain separately gated, not blockers."

### 2026-07-25 — M9 SCOPED: layer-1 role-aware directed traversal package

- **Decider:** Nasser, executed by Opus 5.
- **Question:** "M9" has floated informally since the NexusVerifier findings doc (finding #5) as
  shorthand for "if a layer-1 `Topos.Hypergraph.Knowledge` package ever gets built" — but it was
  never part of the locked roadmap (`docs/SPECIFICATION.md` §6 is explicitly "M0 through M8,
  structure LOCKED") and had no defined scope or exit criterion. Does it warrant becoming a real
  milestone now, and if so, what exactly does it contain?
- **Decision: yes, scope it as a real M9, extending (not reopening) the locked M0–M8 structure.**
  The forcing evidence is stronger than finding #5 originally documented — investigating this
  decision surfaced a **third** independent reinvention of the identical pattern, not just the
  two (ChatMemory, NexusVerifier) already on record:
  1. `samples/Topos.Samples.ChatMemory/ChatMemory.cs:81-85` (`EntitiesMentionedIn`) — one-hop:
     `GetVertexHyperedges(turn)` → `IncidencesFrom(edge)` → `.Where(i => i.Role == MentionedRole)`.
  2. `NexusVerifier/NexusAgent/NexusAgent.ToposExperiment/NarySearch/NaryBackwardChainer.cs`
     (`CandidateEdgesFor`, `SubgoalsOf`, `[verified:src]` 2026-07-25) — multi-hop: role-gated
     AND-OR recursive search, filtering `IncidencesOf`/`IncidencesFrom` by `BeforeRole`/`AfterRole`.
  3. **`Rich-Learning-Base/src/RichLearning.V2/Learning/ToposGraphProjection.cs`
     (`DirectedBfs`, `DirectedShortestPath`, `[verified:src]` 2026-07-25)** — not previously cited
     in the findings doc. This is the important one: its two private traversal methods are
     **already written entirely against `IHypergraphQuery`/`Handle`/`Incidence` — zero RLB types**
     — a full generic directed-BFS-and-shortest-path implementation over role-tagged hyperedge
     membership, hardcoded to `AnchorRole=0`/`TargetRole=2` only because RLB's `HyperEdgeRole`
     happens to use those values. It is, functionally, already M9's core — built once, outside
     Topos, because Topos didn't yet offer it.
  - **Scope:** a new package, `Topos.Hypergraph.Knowledge` (own assembly — matching the M4
    packaging-split precedent of splitting at a real architectural boundary, and keeping the
    kernel itself untouched: "the kernel does not judge" stays intact, this package is where
    judgment lives). Contents, generalizing what already exists in the three consumers above:
    - `DirectedBfs(IHypergraphQuery graph, Handle start, byte fromRole, byte toRole)` —
      generalizes `ToposGraphProjection.DirectedBfs`, parameterized instead of hardcoded.
    - `DirectedShortestPath(IHypergraphQuery graph, Handle from, Handle to, byte fromRole, byte toRole)`
      — same generalization of `DirectedShortestPath`.
    - `RoleFilteredMembers(IHypergraphQuery graph, Handle vertex, byte role)` — the one-hop case,
      generalizing `ChatMemory.EntitiesMentionedIn`'s pattern so it stops being hand-rolled LINQ.
    - The `docs/ROLE_CONVENTIONS.md` byte-backed-enum pattern (M8, finding #3) becomes real code
      here rather than documentation-only: an `AddIncidence<TRole>` extension over
      `HypergraphKernel` where `TRole : unmanaged, Enum`, so the new traversal methods can also
      offer `TRole`-typed overloads instead of raw `byte` — closing the loop finding #3 opened.
    - No kernel changes. Everything above is expressible purely through `IHypergraphQuery`'s
      existing public surface — consistent with M8 having just locked that surface.
  - **Exit criterion:** at least one real consumer's hand-rolled version is replaced by a call
    into this package, proving it's a genuine drop-in rather than parallel scaffolding — the same
    falsifiability standard M5 set for the kernel itself. `RichLearning.V2.Learning.
    ToposGraphProjection` is the natural first target: refactor its two private methods to call
    `Topos.Hypergraph.Knowledge` instead of maintaining its own copy, then confirm RLB's 346-test
    suite still passes unchanged.
  - **Not in scope:** anything RLB-specific (`IGraphMemory`, `StateKey`, `HyperEdge`) — those stay
    exactly where they are, in RLB. M9 only lifts the generic traversal *engine* those types feed
    into, mirroring how `ToposGraphProjection.BuildAsync` already separates "build a Topos kernel
    from my domain data" (stays in RLB) from "walk it by role" (moves to Topos).
- **Rationale:** this is the opposite of speculative — the code this milestone would add has
  already been written three times independently, by two different projects, against nothing
  but Topos's existing public API. That's a stronger forcing signal than any other pre-M9 item
  (HIF interchange, docs site) had, and unlike those, implementing it costs little: it's an
  extraction and generalization of ~40 already-working lines, not new design.
- **What it changes:** `docs/SPECIFICATION.md` §6 gains an M9 row (the roadmap's locked structure
  is *extended*, not reopened — M0–M8's own scopes/exit-criteria are untouched). No code yet;
  implementation is a separate, not-yet-started step.

### 2026-07-26 — M9 IMPLEMENTED: `Topos.Hypergraph.Knowledge` built, exit criterion met

- **Decider:** Nasser (explicit go-ahead to start M9 and to do the RLB refactor in the same
  session), executed by Sonnet 5.
- **Question:** M9 was scoped but not started (previous entry). Build it, and if so, complete the
  exit criterion (refactor RLB's `ToposGraphProjection` to consume it) in the same pass, or defer
  the RLB side to a separate session?
- **Decision:** built the full scoped surface and completed the exit criterion in one pass — see
  below for what shipped.
- **What shipped:**
  - New package `src/Topos.Hypergraph.Knowledge/` (own assembly, `ProjectReference` to
    `Topos.Hypergraph` only — no kernel changes, per scope):
    - `DirectedTraversal.cs` — `DirectedBfs`/`DirectedShortestPath`/`RoleFilteredMembers`, written
      as extension methods on `IHypergraphQuery` (nicer call-site ergonomics than a plain static
      method; still pure consumer of the public surface, not new kernel API). Directly generalizes
      RLB's `ToposGraphProjection.DirectedBfs`/`DirectedShortestPath` (parameterized `fromRole`/
      `toRole` instead of hardcoded Anchor=0/Target=2) and `ChatMemory.EntitiesMentionedIn`
      (`RoleFilteredMembers`).
    - `RoleExtensions.cs` — `AddIncidence<TRole>` plus `TRole`-typed overloads of the three
      traversal methods, `where TRole : unmanaged, Enum`, turning `docs/ROLE_CONVENTIONS.md`'s
      byte-backed-enum convention into real code. Converts via `Unsafe.As<TRole, byte>` (the "free"
      cast the convention doc describes) but throws `ArgumentException` if `TRole`'s underlying
      type isn't actually 1 byte, rather than silently truncating a wider enum — a real caller
      mistake worth surfacing, not a case to paper over.
  - `tests/Topos.Hypergraph.Knowledge.Tests/` — 11 new tests: Anchor/Condition/Target chain
    traversal (mirroring `HypergraphKernelTests.NAryHyperedge_RoundTripsAllMembers_InOrdinalOrder`,
    per the previous entry's own citation), explicit proof that Condition members are excluded
    from directed reachability, unreachable/self-path edge cases, `RoleFilteredMembers` parity with
    `ChatMemory.EntitiesMentionedIn`'s shape, and the `TRole` generic overloads (including the
    non-byte-backed-enum throw path).
  - **Exit criterion met:** `Rich-Learning-Base/src/RichLearning.V2/Learning/
    ToposGraphProjection.cs` refactored to call `Topos.Hypergraph.Knowledge.DirectedTraversal`
    instead of maintaining its own private `DirectedBfs`/`DirectedShortestPath`/`Reconstruct` —
    those three methods are deleted from RLB entirely. `RichLearning.V2.csproj` gained a second
    `ProjectReference` to `Topos.Hypergraph.Knowledge`. **RLB's 346-test suite passes unchanged**
    (`dotnet test tests/RichLearning.V2.Tests`), confirming the drop-in replacement is behaviorally
    identical, not parallel scaffolding — the same falsifiability standard M5 set.
  - Full Topos suite (`dotnet test Topos.sln`, GDS-oracle suite excluded — needs its Docker
    container, unrelated to this change) green: 141 kernel + 18 persistence + 9 ChatMemory + 11 new
    Knowledge = 179 tests, up from 177 before M9.
- **Rationale:** the previous entry already established this was extraction-and-generalization of
  already-working code, not new design — the two passes (build + RLB refactor) together are the
  same low-risk shape, and Nasser asked for both in one session rather than splitting them.
- **What it changes:** `src/Topos.Hypergraph.Knowledge/` (new), `tests/
  Topos.Hypergraph.Knowledge.Tests/` (new), `Topos.sln` (both added), `Rich-Learning-Base/src/
  RichLearning.V2/Learning/ToposGraphProjection.cs` (refactored, three private methods deleted),
  `Rich-Learning-Base/src/RichLearning.V2/RichLearning.V2.csproj` (new `ProjectReference`). No
  changes to `Topos.Hypergraph`'s kernel or public surface — M8's API-stability freeze stands.

### 2026-07-27 — M10 APPROVED AND IMPLEMENTED: `Topos.Hypergraph.Mcp` server built, dogfooded via raw JSON-RPC

- **Decider:** Nasser (explicit go/no-go on `docs/MCP_SERVER_SPEC.md`, then explicit answers on
  all four §5 forks that matter for v1), executed by Sonnet 5 in the same session.
- **Question:** GLM-5.2's overnight M10 proposal (`docs/MCP_SERVER_SPEC.md`) framed the
  forcing-function case for an MCP server but explicitly left the go/no-go and all five §5 design
  forks to Nasser. Before asking, this session verified the proposal's two load-bearing claims
  rather than trusting them: (1) is `ModelContextProtocol` on NuGet real and net10.0-compatible —
  yes, v1.4.1 as of 2026-07-09, confirmed via the NuGet listing; (2) is the proposal's "License:
  MIT" claim correct — **no, it's Apache-2.0** (still compatible with Topos's MIT license as a
  dependency, but the spec's own `[verified:web]` tag was wrong). Also surfaced a live
  counter-precedent from this session's sibling-project context: `~/Projects/FSDE/src/
  Fsde.McpServer/` already runs an MCP server today and deliberately does **not** use the official
  SDK — it hand-rolled its own JSON-RPC/tool-dispatch layer. Presented both findings to Nasser
  before asking go/no-go.
- **Decision:** **Approve, build v1 today**, using the official SDK (not FSDE's hand-rolled
  approach — that fork was asked and answered explicitly, not assumed). All four §5 forks that
  gate v1 scope resolved to the spec's own tentative leans, confirmed rather than silently adopted:
  - **(a) State model:** stateful, single-session — one `HypergraphKernel` per server process,
    no persistence, lost on exit.
  - **(b) Transport:** stdio only.
  - **(d) Handle wire format:** opaque string (`"#3"`/`"#3g1"`, matching `Handle.ToString()`).
  - **(e) Property values:** tagged union (`{Type, StringValue, NumberValue, BoolValue,
    EmbeddingValue}`), not untyped JSON.
  - **(c) Package boundary** wasn't asked separately — adopted the spec's in-repo lean
    (`src/Topos.Hypergraph.Mcp/`) without a question, since it's the same low-stakes call M4/M9
    already set precedent for and had no plausible counter-argument raised.
- **What shipped:**
  - New package `src/Topos.Hypergraph.Mcp/` (`ProjectReference` to `Topos.Hypergraph` and
    `Topos.Hypergraph.Knowledge`; `PackageReference` to `ModelContextProtocol` 1.4.1 — no kernel
    changes, per scope): `TypeMapping.cs` (Handle wire parsing, the `PropertyValue` tagged union,
    DTOs), `ToposMcpServer.cs` (a `sealed class` — `WithTools<T>()` rejects a `static class` type
    argument, the one place the spec's own sketch needed a real-world correction — with a
    `[McpServerToolType]` static-method tool surface: 18 tools covering vertex/incidence CRUD,
    typed property get/set/remove, kernel-level query (`is_reachable`/`shortest_path`/`bfs`/
    `connected_components`), M9's `directed_bfs`/`directed_shortest_path`/`role_filtered_members`,
    and `semantic_recall`), `Transport/StdioHost.cs` (the ~15-line stdio host). Deliberately not
    exposed, per spec §4: `RestoreVertex`, `AllIncidences`, `HasCycle`.
  - Tool return values are JSON-serialized to `string` rather than assumed to auto-serialize as
    structured content — matching a real convention observed in the SDK's own `EverythingServer`
    sample (`PrintEnvTool` does the same), found by checking the sample rather than guessing.
  - `resolve_property` from the spec's §4 sketch was dropped from the tool surface (not a v1
    deviation worth asking about) — `ResolveProperty<T>` is a cheap dictionary lookup per
    `HypergraphKernel`'s own doc, so each typed `set_property`/`get_property`/`remove_property`
    call just resolves inline; exposing a separate tool returning an "opaque id" nobody consumes
    would have added agent-facing surface for no behavior.
  - `tests/Topos.Hypergraph.Mcp.Tests/` — 13 tests calling the tool methods directly (not through a
    live transport): the full README TripRole worked example end-to-end (create vertices, one
    n-ary hyperedge, `bfs`/`is_reachable`/`directed_bfs`/`role_filtered_members`/
    `directed_shortest_path`), CRUD round-trips for all four property types, `semantic_recall`,
    and error paths (malformed handle string, unknown property type).
  - `samples/Topos.Samples.McpAgent/` — `.mcp.json.example` + a short README (not a C# project,
    per spec §6 item 7's "tiny agent config" framing).
  - **Dogfooded via raw JSON-RPC over the real stdio transport** (not yet through a live
    MCP-aware-agent session — that needs Nasser to restart Claude Code so it picks up the new
    `topos` entry added to this repo's `.mcp.json`): sent real `initialize` / `tools/list` /
    `tools/call` frames by hand, confirmed all 18 tools register with correct schemas, and
    confirmed `create_vertex` → `add_incidence` → `is_reachable` behaves exactly per the kernel's
    documented semantics (including a case that looks surprising until you read
    `IHypergraphQuery.GetBfs`'s doc: the hyperedge vertex itself is never reachable from its own
    members, since BFS only ever yields `Member`s, not the `Source` vertex — the wrapper faithfully
    reproduces this, it isn't a wrapper bug).
  - Full suite green: `dotnet test Topos.sln` — 141 kernel + 18 persistence + 9 ChatMemory + 11
    Knowledge + 13 new Mcp = 192 tests (excluding the GDS-oracle suite's separate 9, which needs
    its Docker container and is unaffected by this change).
- **Rationale:** the spec's forcing-function case (§2) was accepted as sufficient by Nasser without
  requiring a named ready consumer first — a deliberate departure from M9's stricter
  three-independent-reinventions evidence bar, made consciously (§9's honest-risks section named
  this distinction) rather than by default.
- **What it changes:** `src/Topos.Hypergraph.Mcp/` (new), `tests/Topos.Hypergraph.Mcp.Tests/`
  (new), `samples/Topos.Samples.McpAgent/` (new), `Topos.sln` (both new projects added), `.mcp.json`
  (new `topos` entry, additive — the existing `fsde` entry is untouched). No changes to
  `Topos.Hypergraph`'s or `Topos.Hypergraph.Knowledge`'s public surface — M8's API-stability
  freeze stands; this is a pure consumer, same as RLB.
- **What's still open, explicitly not this pass:** literal agent-in-the-loop dogfood (needs Nasser
  to restart Claude Code and drive a couple of tool calls through the live `topos` MCP entry — the
  raw-JSON-RPC dogfood above proves the wiring works but isn't the same as an agent doing it), the
  `MCP_SERVER_SPEC.md`/`SPECIFICATION.md`/`README.md`/`AGENTS.md`/`SESSION_HANDOFF.md` status
  updates (tracked separately, same session), and everything §7 of the spec named as deliberately
  out of scope (HTTP/SSE transport, multi-tenancy, auto-persistence, NuGet publish of the Mcp
  package).

---

### 2026-07-29 — NuGet PUBLISHED: `Topos.Hypergraph`, `.Persistence`, `.Knowledge` live under MIT

- **Decider:** Nasser, executed by Sonnet 5 in the same session.
- **Question:** `docs/NUGET_PUBLISH_CHECKLIST.md`'s gated item from "M8 CLOSED" — license file,
  package metadata, and actually publishing — waited on Nasser deciding a license and a
  public-release timing.
- **Decision:** License **MIT** (already recorded in the checklist). Publish via **GitHub Actions
  + NuGet Trusted Publishing (OIDC)** rather than a long-lived API key, so no NuGet credential is
  ever stored or typed. The repo was also flipped from private to public the same session — partly
  because the package's `RepositoryUrl`/`PackageProjectUrl` would otherwise 404 for consumers, and
  partly because GitHub's required-reviewer environment-protection rule (the approval gate on the
  publish workflow) isn't available on a private repo under GitHub's free plan.
- **What shipped:**
  - `LICENSE` (MIT) at repo root; `PackageLicenseExpression`, `RepositoryUrl`, `RepositoryType`,
    `PackageProjectUrl`, `PackageTags`, `PackageReadmeFile` added to all three publishable csprojs
    (`Topos.Hypergraph`, `.Persistence`, `.Knowledge` — not `Topos.Hypergraph.Mcp`, which stays
    source-only for now).
  - `.github/workflows/publish-nuget.yml` — manual-only (`workflow_dispatch`), with a typed
    `confirm` input and a `dry-run` mode (build + pack + inspect metadata, never contacts
    nuget.org) alongside `publish`. Uses `NuGet/login@v1` for the OIDC token exchange; the
    `NUGET_USER` repo secret holds only the nuget.org username, never a key.
  - A `nuget-publish` GitHub Actions environment gates the job.
  - Published: `Topos.Hypergraph` `0.1.0-m8`, `Topos.Hypergraph.Persistence` `0.1.0-m8`,
    `Topos.Hypergraph.Knowledge` `0.1.0-m9` — verified live by downloading each `.nupkg` directly
    from `api.nuget.org` and inspecting its `.nuspec` and `lib/` contents (not just trusting the
    push-step log), after a `.symbols.nupkg`-vs-real-package push-ordering anomaly on the Knowledge
    package raised (and then ruled out) a concern about which content actually landed.
  - `README.md` and `docs/GETTING_STARTED.md` flipped from "not yet on NuGet" to the real
    `dotnet add package Topos.Hypergraph --prerelease` install line, `ProjectReference` kept as the
    documented way to track `main`.
- **Rationale:** Trusted Publishing was chosen over a classic API key specifically because a prior
  incident in this repo's own history (`docs/GDS_ORACLE_SETUP.md` — a Neo4j password typed into a
  chat session, rotated on principle) made "never type a credential into an agent-observed channel"
  a hard constraint for this session, and OIDC-based publishing satisfies that natively rather than
  requiring discipline to maintain it.
- **What it changes:** `LICENSE` (new), the three csprojs (metadata only, no code changes),
  `.github/workflows/publish-nuget.yml` (new), `.gitignore` (`nupkgs/` added), repo visibility
  (private → public), `README.md` / `docs/GETTING_STARTED.md` (install instructions updated).

---

### 2026-07-30 — M11 PHASE 1 APPROVED AND IMPLEMENTED: Centrality, PageRank, Directed SCC

- **Decider:** Nasser, executed by Sonnet 5 in the same session.
- **Question:** `docs/ALGORITHM_SPEC.md` (authored by GLM-5.2, a fresh survey-driven proposal)
  named six must-have algorithm gaps for M11. Three need only the existing GDS oracle
  (Directed SCC, Centrality, PageRank); the other three (s-connected-components, s-line-graph,
  s-diameter) need a brand-new HNX oracle test project (Docker sidecar) that doesn't exist yet.
  The spec also left four open forks (§6) for Nasser: how much to build now, how to stand up the
  HNX oracle, which adjacency PageRank should use, and whether to open a new milestone.
- **Decision:** Phase 1 only — the three GDS-only items. HNX oracle setup (and the s-walk family)
  deferred, not scoped this pass. PageRank: symmetric, kernel-level (matches spec §6.3's own
  recommendation). Milestone bookkeeping: yes, a new M11 entry (§6.4's recommendation), split into
  "phase 1"/"phase 2" rather than one M11 that silently only covers half the must-have list.
- **What shipped:**
  - `Centrality` (`src/Topos.Hypergraph/Centrality.cs`) — `Degree`/`Closeness`/`Betweenness`, all
    over `BipartiteAdjacency` (the M6 analytics family's adjacency — Modularity/TriangleCount/
    LabelPropagation's, not M1's `GetBfs`/`GetShortestPathLength` member-only convention). This is
    a deliberate deviation from the spec's literal "Built on `GetShortestPathLength`" phrasing for
    Closeness — chosen because it lets Centrality reuse `CliqueExpansionProjectionEngine`, the
    exact same oracle projection Modularity/TriangleCount/LabelPropagation already use, with zero
    new projection machinery and no doubled-bipartite-hop-count correction. Closeness uses GDS's
    actual default formula (`k / Σdistance`, i.e. `useWassermanFaustFormula = false`), confirmed
    via web search against Neo4j's own docs, not the spec's looser "reciprocal of the sum" phrasing
    — chosen because the whole point of the oracle is to match what GDS actually computes.
  - `PageRank` (`src/Topos.Hypergraph/PageRank.cs`) — standard power iteration over the same
    `BipartiteAdjacency`, damping 0.85 default (matches `gds.pageRank`'s own default), uniform
    dangling-mass redistribution.
  - `DirectedScc` (`src/Topos.Hypergraph.Knowledge/DirectedTraversal.cs` + typed-role overload in
    `RoleExtensions.cs`) — iterative Tarjan (explicit work-stack, not recursive, matching the
    kernel's own `GetDfs` discipline) over the role-filtered `fromRole`→`toRole` adjacency
    `DirectedBfs` already walks.
  - 30 new unit tests with hand-computed golden values (`CentralityTests`: K4/bowtie
    degree+closeness+betweenness including a from-scratch Wasserman-Faust derivation;
    `PageRankTests`: a hand-solved bowtie fixed-point via the graph's mirror symmetry;
    `DirectedSccTests`: 3-cycle/acyclic-chain/Condition-exclusion/partition-invariant cases).
  - 6 new GDS-oracle parity tests (`CentralityGdsParityTests`, `PageRankGdsParityTests`,
    `DirectedSccGdsParityTests` in `tests/Topos.Tests.GdsOracle`, which gained a
    `Topos.Hypergraph.Knowledge` project reference for the SCC test). Degree/Betweenness/PageRank
    assert exact-value parity; Closeness asserts a ranking invariant only (same
    weaker-but-robust-invariant move `AnalyticsGdsParityTests`' LabelPropagation test already makes
    for its own non-unique-fixed-point reason) — **because no live Neo4j+GDS instance was reachable
    in the authoring environment (Docker daemon not running) to independently confirm the exact
    formula before committing to an exact-value assertion.** All six soft-skip cleanly under that
    condition, per the existing convention; they should be spot-checked once the oracle is
    reachable, and Closeness tightened to exact-value if it holds.
  - Full suite: 229/229 passing (up from 177), zero regressions.
- **Rationale:** all three items were already independently verified absent before implementation
  (`IHypergraphQuery.cs`, `SWalk.cs`, `DirectedTraversal.cs` read directly, not just trusted from
  the gap-list doc). Reusing the existing `CliqueExpansionProjectionEngine` for Centrality/PageRank
  (rather than building a new bipartite-hop-doubling-aware comparison) was chosen for a
  substantially cleaner, lower-risk oracle story, at the cost of a small deviation from the spec's
  literal adjacency phrasing — judged acceptable since the spec explicitly delegates
  implementation-detail judgment calls to the executing session and the M6-family adjacency is
  already an established, tested Topos convention.
- **What it changes:** `src/Topos.Hypergraph/Centrality.cs` (new), `src/Topos.Hypergraph/PageRank.cs`
  (new), `src/Topos.Hypergraph.Knowledge/DirectedTraversal.cs` (`DirectedScc` added),
  `src/Topos.Hypergraph.Knowledge/RoleExtensions.cs` (`DirectedScc<TRole>` added),
  `tests/Topos.Hypergraph.Tests/{CentralityTests,PageRankTests}.cs` (new),
  `tests/Topos.Hypergraph.Knowledge.Tests/DirectedSccTests.cs` (new),
  `tests/Topos.Tests.GdsOracle/{CentralityGdsParityTests,PageRankGdsParityTests,DirectedSccGdsParityTests}.cs`
  (new) and that project's `.csproj` (new `Topos.Hypergraph.Knowledge` reference). No changes to
  `Topos.Hypergraph`'s or `Topos.Hypergraph.Knowledge`'s existing public surface — purely additive.
- **What's still open, explicitly not this pass:** M11 phase 2 (s-connected-components,
  s-line-graph, s-diameter — needs the HNX oracle project scoped in spec §3.1/§6.1, not started);
  a real consumer adopting the new surface (M11's own exit criterion, spec §7 — NexusVerifier's
  hand-rolled cycle guard replaced by `DirectedScc` is the obvious next step, per finding #4);
  live-GDS spot-check of the six new oracle parity tests (no Docker/Neo4j reachable this session);
  Modularity/HIF/LCC (spec's good-to-have bucket, explicitly out of scope for phase 1).

---

### 2026-08-03 — M11 PHASE 1: live-GDS spot-check, in-repo consumer, and docs

- **Decider:** Nasser, executed by Sonnet 5 in the same session. Continuation of the 2026-07-30
  M11 phase 1 entry above — closes two of that entry's four "still open" items (the live-GDS
  spot-check, and an in-repo real consumer) and adds the example/documentation surface the M11
  work itself never got.
- **Question:** the prior entry shipped Centrality/PageRank/DirectedScc with tests that soft-skip
  without a reachable Neo4j+GDS instance, and left the algorithms undocumented outside their own
  XML comments and the proposal docs (no `API_REFERENCE.md`/`USAGE_PATTERNS.md` entries, no
  consumer using them in this repo). Docker was available this session — worth actually running
  the oracle rather than leaving the spot-check open indefinitely.
- **What shipped:**
  - **Live-GDS spot-check** (`docker start topos-gds-oracle`, per `docs/GDS_ORACLE_SETUP.md`):
    all 6 M11 parity tests now run against a real Neo4j+GDS instance instead of soft-skipping.
    Two real findings, not just a clean pass:
    1. **Closeness matches exactly** — `CentralityGdsParityTests.Closeness_MatchesGdsCloseness_OnBowtie`
       (renamed from `..._AgreesWithGdsOnWhichVertexRanksHighest_...`) now asserts exact-value
       parity to 10 decimal places, confirming `gds.closeness`'s `useWassermanFaustFormula = false`
       default really does match `Centrality.Closeness`'s formula. The prior ranking-only assertion
       is superseded — this is the exact tightening the 2026-07-30 entry called for once an
       instance was reachable.
    2. **PageRank needed a real fix, not just a tightening.** `gds.pageRank`'s default output is
       **not** a probability distribution — its base term is the classic `(1-d)` per node, not
       `(1-d)/N` — so raw GDS scores sum to ~N rather than 1 (empirically: every score was ~5×
       `PageRank.Compute`'s matching value on the file's 5-vertex bowtie). This is a genuine
       convention difference between the two implementations, not a bug in either: `PageRank.Compute`'s
       "sums to 1.0" contract is its own documented, intentional choice (matches the conventional
       "probability distribution" framing of PageRank). `PageRankGdsParityTests` now L1-normalizes
       GDS's raw scores (divide by their own sum) before comparing, so the assertion is on the
       *relative* distribution both implementations actually agree on. Full rationale is inline in
       the test's class doc.
  - **A real in-repo consumer** (partially closes the M11 exit criterion, spec §7 — the cross-repo
    half, RLB/NexusVerifier adopting the surface, remains genuinely open; that's other repos, out
    of scope here): `samples/Topos.Samples.ChatMemory` gained three consumer methods —
    `MostConnectedEntities` (`Centrality.Degree`), `RankByImportance` (`PageRank.Compute`), and
    `RecordDerivation`/`DetectCircularDerivations` (`DirectedTraversal.DirectedScc`, catching
    circular fact-derivation — the same class of bug NexusVerifier finding #4 describes, applied to
    this domain's own derivation shape). `ChatMemory.csproj` gained a `Topos.Hypergraph.Knowledge`
    project reference. 4 new tests in `ChatMemoryTests.cs` (13/13 passing, up from 9).
  - **Documentation:** `docs/API_REFERENCE.md` gained `### Centrality` and `### PageRank` entries
    under Analytics, and `DirectedScc` was added to the existing `DirectedTraversal`/`RoleExtensions`
    entries under Knowledge. `docs/USAGE_PATTERNS.md` gained "Pattern 9 — Ranking and structural
    analysis," covering all three algorithms with the ChatMemory methods as worked examples,
    including an explicit "don't confuse `DirectedScc` with `HasCycle`" callout (the same mistake
    Pattern 8 already warns about, repeated here because it's the single most common one).
  - **Library research:** already comprehensively covered by the pre-existing
    `docs/ALGORITHM_SURVEY.md`/`ALGORITHM_GAP_LIST.md`/`ALGORITHM_SPEC.md` (GDS as oracle;
    yamafaktory/hypergraph as implementation provenance for all three algorithms) — not redone
    this pass, only cross-referenced from the new doc sections.
- **Full suite:** 233/233 passing (ChatMemory sample: 13, up from 9; kernel/Knowledge/persistence
  suites unchanged; GDS-oracle: 15/15 live, up from soft-skipping).
- **What's still open:** M11 phase 2 (s-connected-components/s-line-graph/s-diameter, needs the
  HNX oracle project, unchanged from the prior entry); the cross-repo half of the M11 exit
  criterion (RLB's `ToposGraphProjection` and NexusVerifier's chainer actually adopting the new
  surface — different repos, not touched this session); Modularity/HIF/LCC (still good-to-have,
  out of scope).

---

### 2026-08-03 — NUGET VERSION SCHEME CORRECTED: `-mN` → `-m.N`, base `0.1.0` → `0.2.0`

- **Decider:** Nasser, executed by Opus 5. Corrects an assessment this log and
  `docs/NUGET_PUBLISH_CHECKLIST.md` both previously got wrong.
- **The defect:** the milestone-prerelease convention `0.1.0-mN` **sorts in the wrong order**.
  SemVer 2.0 compares a prerelease tag as dot-separated identifiers; an undotted `m11` is a
  single alphanumeric identifier compared in ASCII order, so character-by-character `m11 < m8`
  (`'1' < '8'`) and `m10 < m9`. Verified against NuGet's own `VersionComparer` (not read off the
  spec): the true order of what is published is `0.1.0-m11 < 0.1.0-m8 < 0.1.0-m9`.
- **Live consequence, observed not theorised:** after publishing `0.1.0-m11` earlier the same
  day, `dotnet add package Topos.Hypergraph --prerelease` **still resolved to `0.1.0-m8`** — the
  newly-published packages were unreachable via the normal install path. Mixing an explicit
  `--version 0.1.0-m11` with the older `Knowledge 0.1.0-m9` (which depends on
  `Topos.Hypergraph >= 0.1.0-m8`) produced a hard `NU1605 Detected package downgrade` failure.
  **This was misdiagnosed once before being understood:** during the publish session the
  `--prerelease` install returning `m8` was attributed to nuget.org CDN propagation lag and
  waved through. It was not lag; it was this bug. Latent since `m10` (the first double-digit
  milestone, 2026-07-27); first surfaced by the cross-package M11 bump.
- **Decision — two changes, the second is load-bearing:**
  1. **Dot the suffix** (`-m.11`), making the numeric part its own identifier, compared
     numerically: `m.8 < m.9 < m.10 < m.11 < m.100` ✅
  2. **Bump the base version `0.1.0` → `0.2.0`.** Dotting *alone does not fix it*, because every
     `m.N` sorts *below* every already-published `mN` — verified: `0.1.0-m.99 < 0.1.0-m8`, since
     the first identifier `"m"` sorts below `"m8"` lexically. Only the base-version bump clears
     the previously-published set. A minor (not patch) bump also matches what actually shipped:
     M11 phase 1 added new public API (`Centrality`, `PageRank`, `DirectedScc`).
- **New versions:** `Topos.Hypergraph` `0.2.0-m.11`, `Topos.Hypergraph.Knowledge` `0.2.0-m.11`,
  `Topos.Hypergraph.Persistence` `0.2.0-m.8` (milestone stays 8 — that package's code is
  unchanged since M8; only the base moves, which is what clears its published `0.1.0-m8`),
  `Topos.Hypergraph.Mcp` `0.2.0-m.10` (never published; bumped for scheme consistency). Each
  csproj carries an inline comment explaining why the dot is required, so the next person to
  touch a `<Version>` does not silently undo it.
- **What could NOT be fixed:** the already-published `0.1.0-m8`/`-m9`/`-m11` versions. NuGet
  versions can be unlisted but never deleted or re-pushed — the strings are permanently consumed
  and permanently mis-ordered. Unlisting them is a separate call, not made here.
- **Process lesson, recorded because it is the actual root cause:** `NUGET_PUBLISH_CHECKLIST.md`
  §4 explicitly certified these versions "✅ valid (prerelease tag `m8` after `-`)" and
  recommended publishing them as-is. That check tested *syntactic* validity and never tested
  *ordering* — the property that actually matters. The checklist now carries a correction box
  and a required pre-publish assertion that the new version compares greater than the highest
  published one, using `VersionComparer` rather than eyeballing.
- **What it changes:** `<Version>` in all four `src/*/*.csproj`, the version table + a correction
  box in `docs/NUGET_PUBLISH_CHECKLIST.md` §4 and its §1 summary line, and the install-versions
  paragraph in `docs/Documentation.md` §1. No code, no API, no test changes.
- **Not done here:** republishing to NuGet under the new versions (a separate, permanent action
  requiring an explicit go-ahead), and unlisting the mis-ordered `0.1.0-m*` versions.

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
