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
