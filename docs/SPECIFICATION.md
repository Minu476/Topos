# Topos — Specification
### A typed-property hypergraph kernel for C#, purpose-fit for AI/agent memory

**Status:** Draft for GPT and Claude review · **Date:** 2026-07-23
**Author:** ZCode (GLM-5.2) · **Decider:** Nasser Towfigh
**Build strategy (decided 2026-07-23, `docs/DECISIONS.md` §6):** Topos is built as
**Rich-Learning-Base's kernel first**, with standalone-library ambition as a falsifiable M5
milestone (a non-RLB second consumer).

---

> ## How to review this document (for GPT and Claude)
>
> This is the consolidated specification synthesized from `BASE_INVESTIGATION.md` (the
> 10-library survey + proposed contract), `AGENT_MEMORY_COMPETITORS.md` (the
> Zep/mem0/Letta/Cognee survey), and `DECISIONS.md` (the two-reviewer synthesis). It is
> written so a reviewer can adjudicate specific decisions, not just react to prose.
>
> **Conventions:**
> - Every claim carries a provenance tag — `[verified:src=path]`, `[verified:spec=§]`,
>   `[verified:docs=url]`, `[verified:web=url]`, or `[unverified:inferred]`. Same discipline as
>   the two investigations. No unsourced assertions.
> - **Decisions are marked `🔒 LOCKED`** when already adjudicated by Nasser or by two-reviewer
>   consensus (from `DECISIONS.md`). Do not re-litigate without new evidence.
> - **Open points are marked `🟡 OPEN`** with a specific question. **This is where your input
>   matters most.** A consolidated `§12 — Open questions for this review` lists them all.
>
> **What we need from you:**
> 1. Pressure-test the `🟡 OPEN` points — these are the live decisions.
> 2. Sanity-check the locked decisions; flag any that look wrong in retrospect.
> 3. Check internal consistency between the contract (§3), the layer architecture (§4), and the
>    roadmap (§6) — they were written separately and may have seams.
> 4. Flag any place the spec overclaims vs. what the evidence actually supports (we already
>    caught and corrected one such overclaim in the competitor survey — see `§10.2`).

---

## 1. Why Topos exists — the thesis

> **The thesis, in three lines:**
>
> 1. RLB learns over **atomic n-ary events** — a single eligibility decision jointly gated by
>    N Condition members, producing one firing event, one reward signal, and one set of joint
>    statistics.
> 2. Binary-edge decomposition can represent the *topology* of such an event (as a star) but
>    **destroys its atomicity** — joint credit assignment, joint success rates, and the
>    "all-conditions-present" gate have no faithful home on per-leg edges.
> 3. **Therefore the storage substrate should preserve n-ary structure**, rather than
>    reconstruct it lossily from binary edges.
>
> This is a workload argument, not a mathematical-impossibility claim. We don't need to prove
> binary graphs *can't* express n-ary events — only that they produce a *worse implementation*
> for this workload. That is a much stronger, much more falsifiable claim.

### 1.1 The RLB HyperEdge — the workload that grounds the thesis

RLB V2 ships a directed N-ary `HyperEdge` at `src/RichLearning.V2/Models/HyperEdge.cs` (247
lines) — not theoretical scaffolding: **28 dedicated tests pass against it** and it is wired
into the learning loop. `[verified:src=HyperEdge.cs]` `[verified:src=HyperedgeTests.cs — 28 tests]`

Its shape — the design Topos must serve — is **N-ary with role-tagged members**
(`HyperEdgeMember(StateKey Key, HyperEdgeRole Role ∈ {Anchor, Condition, Target}, int Ordinal)`),
locked D2 cardinality (one Anchor, zero-or-more Conditions, one Target), learnable theta
parameters (`v = sigmoid(b + w·x + u·r)`; `ReinforceTheta(decayedReward, lr, ctx)`), and
per-membership statistics (`TransitionCount`/`SuccessRate`/`Confidence` carried on the edge).
`[verified:src=HyperEdge.cs:6-27, 43-46, 100-107, 159-246]`

This maps directly onto Topos's primitives: `Role` → `IncidenceRole`; `Ordinal` →
`Incidence.Ordinal`; theta/confidence → incidence-level cell properties; the N-ary member set
→ the `Incidence` primitive. (§3 finalizes the mapping.)

### 1.2 Why binary can't faithfully express it

The structural argument is **joint eligibility over N members produces one atomic event.**
RLB's hyperedge is a single decision — *"given the Anchor AND all Conditions present, fire
toward the Target"* `[verified:src=HyperEdge.cs:43-46]` — yielding one firing event, one reward
signal, one set of joint statistics. A binary star (Anchor→Target + one Condition→Target edge
per Condition) can encode the topology but not the atomic event semantics:

1. **Credit assignment is joint.** Reinforcement must update one theta for the joint decision;
   decomposition forces either duplicated reward (each leg learns the same signal N times) or
   an external coordinator reconstructing the joint event the binary edges destroyed.
2. **Joint statistics have no per-leg home.** `SuccessRate` of the joint decision is not the
   product of per-leg rates — it's an aggregate over a co-firing set. Attaching it to one edge
   misattributes; duplicating it across the star invites drift.
3. **The hard gate ("ALL Conditions present") isn't the conjunction of independent predicates**
   once learning enters — each Condition edge carries its own theta and fires independently, so
   the joint event becomes emergent rather than stored.

This is **pragmatic non-derivability, evidenced in production code** — not a formal
impossibility proof (we don't have one, and shouldn't claim it). RLB's own `HyperEdge.cs`
corroborates this: its synthesis-equality theorem *breaks* for hyperedge leaves
(`r[3]` discriminator mismatch `[verified:src=HyperEdge.cs:153-157]`) — a symptom of the same
non-derivability, though the load-bearing argument is the Condition-aggregation semantics
above, not that specific code-level discriminator.

### 1.3 The measured results that depend on it

RLB's DAPSA architecture, built on this substrate, has measured results across five domains
`[verified:src=Rich-Learning-Base/INITIAL_README.md]`:

| Domain | Implementation | Measured result |
|---|---|---|
| Chess | StockFish-Fugue | **99.1% win rate** vs Stockfish (depth-limited) |
| Logistics | Fugue-Logistics-CS | **154 deliveries / 0 collisions** across 44 robots at 3.7ms/step (~270 Hz) |
| LLM Reasoning | Rich-Learning (Chimera) | **87% cost reduction** via passive fossil recall |
| Face Recognition | Rich-Learning-Face | Zero-forgetting identity accumulation across sessions |
| Trading | Rich-Learning-Trading | Live alpha extraction; fossilized regime patterns survive transitions |

The cognitive architecture that produced these is built on a hyperedge substrate — measured
evidence that n-ary composition is load-bearing for real long-lived adaptive memory workloads,
not a theoretical preference.

---

## 2. Scope and identity

### 2.1 What Topos is

Topos is an **embedded, in-process, typed-property hypergraph kernel for C#**, optimized for
**long-lived adaptive symbolic memory workloads** — incremental updates, provenance,
explainability, retrieval, symbolic+vector coexistence, stable identities, partial activation,
mutable knowledge that grows rather than being overwritten.
`[verified:docs=docs/BASE_INVESTIGATION.md §1]`

### 2.2 What Topos is NOT (scope boundaries, to prevent creep)

- **Not an extraction pipeline.** Topos stores and queries; it does not run LLMs to extract
  triples from text. (mem0's April-2026 retreat from LLM-driven extraction is the warning
  here — `AGENT_MEMORY_COMPETITORS.md §3.2`. Consumers may build extraction on top; Topos does
  not require it.) `[verified:src=mem0 commit a488e19044e4]`
- **Not a server / hosted product.** Embedded only, like Kuzu/SQLite. The deployment shape the
  agent-memory field selected for (`AGENT_MEMORY_COMPETITORS.md §5.5`).
- **Not a reasoning/entailment engine.** Like a pure hypergraph, Topos has no model-theoretic
  semantics (cf. RDF 1.2's entailment — `BASE_INVESTIGATION.md §3.7`). It is a
  storage/retrieval/traversal structure. An entailment layer could be added later; the spec
  does not include one.
- **Not multi-language by FFI.** Pure C#. The whole point (per RLB's
  `Hypergraph-Port-From-Petgraph.md`) is eliminating the cross-language boundary.
  `[verified:docs=Rich-Learning-Base/ToDoList/Hypergraph-Port-From-Petgraph.md §"what NOT to do"]`
- **Not a belief-revision / entailment engine.** Layer 1 *stores* `fact`/`belief` concepts and
  the asserted/quoted/hypothesized mode flag, and the AI-services capability *attaches*
  provenance/confidence — but Topos does **not** reason over them. No contradiction resolution,
  no truth-maintenance, no logical entailment. Storing the mode flag is storage; revising
  beliefs from it is a consumer's job. (Cf. RDF 1.2's model-theoretic semantics, which Topos
  deliberately does not implement — `BASE_INVESTIGATION.md §3.7`.) Future contributors: do not
  read layer 1 as license to add belief-revision logic to the kernel.

### 2.3 The name and the deeper fork (🟡 OPEN — identity)

The name **Topos** survives either resolution of the deeper identity fork:
- **Hypergraph library** — the conservative reading. Namespace `Topos.Hypergraph`. Built M0–M3
  as a hypergraph; projection layers become later milestones.
- **Typed incidence model** — if the kernel can project to hypergraph /
  property-graph / RDF / relational, then it's an *incidence model* with hypergraph as one
  view. Namespace just `Topos`.

`DECISIONS.md §4.1` leans: **hold as a reach goal, not a founding decision.** Build M0–M3 as a
hypergraph library; revisit the identity at the projection milestone. **🟡 OPEN (Q2):** confirm
this hold. See `§12`.

### 2.4 The horizon: Topos as a persistent symbolic memory substrate (not today's scope)

A deeper framing worth keeping in view: the primitives Topos builds (Vertex, Incidence,
Property, Handle) are ultimately in service of **memory**, not graphs. If, years from now,
someone implemented the Topos contract on SQLite, FoundationDB, an append-only log, or a GPU
tensor, and it still served the same workloads — then hypergraph is one *implementation* of the
deeper abstraction, which is **a persistent symbolic memory substrate**.

This is explicitly **horizon, not founding decision.** The spec does not restructure the
contract around a "Memory Event" primitive today — that would be exactly the overengineering
the project's risk profile (and both reviewers) warn against. The discipline is: **build the
hypergraph, earn the right to generalize.** Many successful infrastructure projects became more
general than originally intended, but only after proving themselves in one domain. Topos's
M5 second-consumer test is the checkpoint where generalization becomes a live question rather
than speculation. Until then, the hypergraph identity stands.

---

## 3. The storage contract (🔒 mostly LOCKED)

The four-primitive + two-invariant shape, synthesized from source-level reading of 10 libraries
and pressure-tested across four rounds. It survived every attempt to add a fifth primitive — a
real but not conclusive signal. `[verified:docs=docs/BASE_INVESTIGATION.md §5.1]`
`[verified:docs=docs/DECISIONS.md §1]`

```
PRIMITIVES (4):
    Handle        — newtype + monotonic never-reused counter (yamafaktory)
                    + generational version bits for stale-handle detection (EnTT)
    Vertex        — Handle + VertexRoles (bitmask) + VertexStatus (reserved hot-path slot)
                    + PropertyBag (columnar, EnTT-style sparse-set pools)
    Incidence     — SourceHandle + MemberHandle + IncidenceRole (byte) + Ordinal (packed struct;
                    HyperNetX-style cell properties attach here)
    PropertyKey<T>— identity (string) separate from PropertyId (int, per-process registry)

INVARIANTS (2):
    1. Dormant never garbage-collected; provenance edges always resolve (even to dormant targets).
       (KaHyPar's disable-flag is the storage pattern; the invariant is the integrity rule.)
    2. VertexRoles and IncidenceRole are independent axes.
       (TypeDB's role model validates this; Anchor/Condition/Target live on the incidence.)
```
`[verified:src=yamafaktory/hypergraph indexes.rs; verified:src=EnTT entity.hpp; verified:src=HyperNetX property_store.py]`

### 3.1 The RLB grounding — the contract is not abstract

Every field maps onto the RLB `HyperEdge` that already runs in production:

| Topos primitive | RLB HyperEdge source | Notes |
|---|---|---|
| `Incidence.IncidenceRole` | `HyperEdgeMember.Role ∈ {Anchor, Condition, Target}` | `[verified:src=HyperEdge.cs:6-19]` |
| `Incidence.Ordinal` | `HyperEdgeMember.Ordinal` | Stable, deterministic member sequence `[verified:src=HyperEdge.cs:24-27]` |
| Incidence-level cell properties | `ThetaParameters`, `TransitionCount`, `SuccessRate`, `Confidence` | Per-membership, non-derivable (§1.2) `[verified:src=HyperEdge.cs:100-115]` |
| N-ary `Incidence` set | `HyperEdge.Members` (role-tagged, N-ary) | D2 cardinality maps to a policy layer `[verified:src=HyperEdge.cs:226-246]` |
| Learnable edge weights | `ReinforceTheta(decayedReward, learningRate, ctx)` | Reward-ascent gradient `[verified:src=HyperEdge.cs:184-222]` |

### 3.2 The five settled questions (🔒 LOCKED)

From `DECISIONS.md §1`, adopted without further debate:

| # | Question | Decision |
|---|---|---|
| 1 | Reserved hot-path slots for `Roles`/`Status`? | **YES** — reserved struct fields. Both read per-hop; a PropertyBag indirection on the inner loop is the wrong tier. |
| 2 | Spectral machinery (Laplacian, KaHyPar)? | **STAY DEFERRED.** M7 stays at the end of the roadmap. |
| 3 | Packaging — one assembly or split? | **ONE through M3; split at M4** (`.Core` + `.Persistence`). |
| 4 | Reification depth cap in the contract? | **NO CAP.** The store records, it doesn't judge. Bounding belongs in the expansion policy. |
| 5 | Embeddings — first-class field or `PropertyKey<T>`? | **`PropertyKey<float[]>`** with ANN index as a separate derived structure. Keeps the kernel symbolically pure. **float, not double** (models emit float32). |

`[verified:docs=docs/DECISIONS.md §1]`

### 3.3 The one divergent question (🟡 OPEN — Q3)

**M5 sequencing:** defer *all* of embeddings/learnable/provenance to M5 (GPT), or **split it** —
primitive *shapes* in M0/M2, *machinery* in M5? `DECISIONS.md §2.1` leans toward the split: the
Incidence primitive's fields are *already* justified by these concerns (cell properties,
`IncidenceRole`, the mode flag lands in M2 anyway), and embeddings as `PropertyKey<float[]>`
from day one costs nothing and forecloses nothing. **🟡 OPEN:** confirm "split it." See `§12`.

### 3.4 Concurrency model (🔒 LOCKED, amended 2026-07-24 — see correction below)

> *Design note: M0's exit criterion requires thread-safety, but a concurrency model is
> expensive to retrofit onto sparse-set pools after the fact — so it belongs in the
> contract, not discovered during M0 implementation. (CSR frozen tiers, when they arrive
> at M4, are immutable post-construction and inherit thread-safety for free.)*

> **🔧 Correction (2026-07-24, measured during M0 implementation):** the original bullet below
> called for copy-on-write (`ImmutableArray`-per-key) on the mutable tier for lock-free reads.
> Built and benchmarked against a naive `Dictionary` baseline (spec's own M0 gate), that choice
> measured **5–6× slower even in the benign case, and O(N²) — thousands of times slower — for a
> hub vertex with many incident members** (55ms to build one 8,000-member hyperedge membership
> set, against a 3.7ms total RLB step budget). Full data:
> `docs/M0_BENCHMARK_RESULTS_2026-07-24.md`. Replaced with a `ReaderWriterLockSlim`-per-pool
> design (uniform across vertices, properties, *and* incidences — see the corrected bullet
> below), which benchmarked ~2.2–2.4× *faster* than naive for the vertex/property pattern and
> eliminated the O(N²) incidence pathology. **This is exactly what the M0 benchmark gate is
> for** — the contract's prior lean on COW was reasonable a priori, and wrong once measured;
> the gate caught it before it became load-bearing.

The first consumer (RLB logistics: 44 robots, ~270Hz `[verified:src=Rich-Learning-Base/INITIAL_README.md]`)
is concurrent by nature. Topos commits to a concurrency model at the contract level so the
storage layout is designed for it from M0:

- **Access model: Single-Writer / Multi-Reader (SWMR) at the kernel boundary.** One mutator
  thread owns writes. Handle allocation is genuinely lock-free (`Interlocked.Increment`); all
  other reads take a per-pool `ReaderWriterLockSlim` read lock — cheap and effectively
  uncontended in the single-writer case, and measurably faster in practice than the
  `ConcurrentDictionary`-based alternative this spec originally called for (see the correction
  above). `[verified:src=docs/M0_BENCHMARK_RESULTS_2026-07-24.md §1]`
- **Counter allocation: lock-free monotonic.** `Handle` generation uses `Interlocked.Increment`
  on a single counter — no lock contention on the hottest path (handle allocation during
  ingestion). `[verified:src=yamafaktory/hypergraph — AtomicU64 counter pattern]`
- **Storage layout: one pattern for every pool, not two.** Vertices, typed properties, *and*
  incidence indexes are each a `SparseSet<T>` (or `SparseSet<List<Incidence>>` for incidences)
  behind its own `ReaderWriterLockSlim`. Reads take an O(k) snapshot copy where needed (k =
  current collection size) rather than paying a copy cost on every write — the right trade for
  write-heavy accumulation, which the fan-in benchmark showed COW gets backwards.
  `[verified:src=src/Topos.Hypergraph/PropertyPool.cs, IncidenceIndex.cs]`
- **Granularity: per-pool, not global.** Each pool has its own lock; writers on different pools
  don't contend. `[verified:src=EnTT registry.hpp — per-pool design, adapted to RWLS rather than
  COW per the correction above]`
- **🟡 OPEN (Q3b — sub-point of the M5 split):** the persistence tier (M4) introduces LSM
  compaction, which relocates physical slots. The per-pool-lock pattern handles this in
  principle, but the exact compaction-vs-read-window protocol is an M4 design decision, not an
  M0 one.

**This locks the axes.** M0's exit criterion "thread-safe... passing a fuzz+concurrency suite"
now has a concrete model to test against, not a blank page — and that model has now been
implemented and measured, not just proposed.

### 3.5 Handle generation-bits: not redundant, but the reason needs stating (🟡 OPEN — Q7)

> *Design note: under a strict reading of Invariant 1 (dormant never GC'd) plus a
> monotonic-never-reused counter, there is no scenario for the generation field to
> disambiguate — so either there's an unstated reuse path (e.g. compaction), or the field is
> dead weight. Worth a one-line resolution before M0 locks the Handle struct layout.*

The resolution: **Invariant 1 guarantees *logical* persistence; generation bits protect
*physical* slot relocation.** Logical Handles never change; physical storage slots may be
reassigned during M4 compaction (LSM defrag, tombstone reclamation) — and *that* is the
scenario EnTT's generation bits exist for. `[verified:src=EnTT entity.hpp — generational IDs
for stale-handle detection]`

But a real tension stands: **for M0 (in-memory, no compaction), the generation field may be
dead weight.** Two options:
- **(a) Include generation bits from M0** — costs a few bits per Handle, but the struct layout
  is stable through M4's compaction addition. Defensive.
- **(b) Add generation bits at M4** when compaction actually appears — leaner M0, but a Handle
  struct layout change mid-roadmap (breaks persisted formats).

**🟡 OPEN (Q7):** (a) or (b)? The lean is (a) — Handle struct layout stability is worth a few
bits, and re-litigating the Handle shape at M4 is the kind of downstream churn the spec exists
to prevent. But this is genuinely Nasser's call (it's a storage-layout decision with perf
implications). See `§12`.

---

## 4. The layer architecture (🔒 LOCKED — layer stack; 🟡 Q10 — top shape)

> *Design note: algorithms are not a single layer on top of the stack — different algorithm
> families operate on different layers (traversal on the graph model; analytics on embeddings
> or projected views; learning on the storage model's edge weights; projection across layers).
> The earlier draft placed "Algorithms" as a layer-5 monolith; the current shape reflects that
> they are cross-cutting capabilities, not a layer. The three-layer *substrate* is locked; the
> capabilities that sit above it are consolidated in §4.2.*

### 4.1 The substrate (🔒 LOCKED) — three layers

```
┌─────────────────────────────────────────────────────────────┐
│  3. Storage model   CSR (frozen tiers — M4) + IndexMap       │
│                     (mutable) + sparse-set property pools +  │
│                     specifics-strategy indirection +         │
│                     tombstoning + the §3.4 concurrency model │
├─────────────────────────────────────────────────────────────┤
│  2. Graph model     Vertex, Incidence, reification via       │
│                     Role:Edge, composable views, set algebra │
├─────────────────────────────────────────────────────────────┤
│  1. Knowledge model typed domain concepts above the graph    │
│                     (Anchor/Condition/Target, fact, belief;  │
│                     cardinality validation lives here)       │
└─────────────────────────────────────────────────────────────┘
```

Each layer's borrow provenance is in `BASE_INVESTIGATION.md §5.2–§5.5`. The Knowledge-model
layer is the one the investigation's structure under-specified; RLB's Anchor/Condition/Target
role model is the first concrete instance of it.

**Where cardinality/validation rules live (🔒 LOCKED).** The kernel (layers 2–3) **does not
judge** — it records. Domain-specific constraints like RLB's D2 cardinality ("exactly one
Anchor, one Target, zero-or-more Conditions," enforced by `HyperEdge.ValidateCardinality`
`[verified:src=HyperEdge.cs:226-246]`) live in **layer 1 (Knowledge model)** as typed domain
concepts, *not* in the storage contract. This resolves the apparent tension with `DECISIONS.md
§1` Q4 ("no cap in the contract — the store records, it doesn't judge"): the contract has no
cap; the *domain type* in layer 1 enforces D2 at construction. This placement directly affects
§12 Q5 (RLB coupling depth): when RLB's `HyperEdge.cs` is rewritten to sit on Topos, its
`ValidateCardinality` logic becomes a layer-1 domain type — the cleanest possible seam for the
port.

### 4.2 Cross-cutting capabilities above the substrate

The capabilities that consume the substrate are **not a stacked layer** — they are independent
families that each operate on whichever layer(s) they need:

| Capability | Operates on | Examples | Milestone |
|---|---|---|---|
| **Traversal** | Graph model (layer 2) | BFS/DFS, reachable, shortest-path, cycle, SCC, s-walk | M1 (M6 for s-walk) |
| **Analytics** | Graph model + projected views | Louvain, Label Propagation, WCC/SCC, Triangle Count, modularity — verified against Neo4j GDS (§5) | M6 |
| **Learning** | Storage model's edge weights (layer 3) | reward-ascent theta updates, confidence tracking | M5 |
| **AI services** | Graph model + storage | embeddings (`PropertyKey<float[]>` + ANN), provenance, confidence, asserted/quoted/hypothesized mode | M5 (shapes M2) |
| **Projection** | All three layers | property-graph view (for GDS), bipartite view, future RDF/relational projections | M1 (GDS), later |

**Why this matters:** treating algorithms as one layer forces artificial uniformity — a
shortest-path routine has nothing in common with a theta-update routine except that both "run on
graphs." Separating them as capabilities lets each evolve on its own cadence and target the
correct layer directly. `[verified:docs=docs/DECISIONS.md §4.2 — the original layered proposal;
this section revises the top shape per §12 Q10.]`

**🟡 OPEN (Q10):** the three-layer substrate is locked; the capability list above is the
proposed shape. Reviewers: does this five-capability split (Traversal/Analytics/Learning/AI
services/Projection) land right, or should any be merged/split? (Note: this revises the earlier
single-"Algorithms"-layer proposal — the substrate is unchanged, only what sits above it.)

---

## 5. Verification strategy — Neo4j GDS as the correctness oracle (🔒 LOCKED)

**Decision (Nasser, 2026-07-23, `DECISIONS.md` §6):** use Neo4j GDS (Graph Data Science) as an
independent oracle for Topos's standard algorithms. This is the Lean-for-NexusVerifier pattern
applied to graph algorithms: a trusted external implementation as ground truth.

- **For each standard algorithm Topos implements** (BFS/DFS/shortest-path/cycle/SCC/PageRank/
  community detection via Louvain/Label-Propagation/WCC/SCC/Triangle-Counting/Local-Clustering-
  Coefficient), a paired test runs the same query against the same graph in Neo4j GDS and
  asserts outputs match (modulo ordering).
- **Property-graph projection of a hypergraph** (via the ProjectionEngine from
  `BASE_INVESTIGATION.md §5.4`) gives a binary-graph view GDS can operate on — the standard
  algorithms verify cleanly through this projection.
- **Where the hypergraph and its projection disagree** (s-walk, role-gated reachability,
  anchor/condition/target semantics), **GDS cannot verify** — Topos's answer is the novel claim
  there. GDS is the oracle for *standard* algorithms only. This is the precise scope of the
  oracle, and the honest limit.
- Neo4j is already in Nasser's stack (RLB's `Neo4jGraphMemory`, FSDE). No new infrastructure;
  the GDS plugin install is the only addition.

### 5.1 GDS licensing — a real consideration, handled by test-only isolation (🔒 LOCKED)

> *Design note: "no new infrastructure" implicitly assumed "free." On investigation this is
> more nuanced than a one-liner — and the licensing is the bigger issue, not the per-algorithm
> tier.*

- **License: GDS Community Edition is GPLv3** (Neo4j's open-core model; Community = GPLv3,
  Enterprise = commercial/AGPLv3 dual-license).
  `[verified:web=neo4j.com/blog/news/open-core-licensing-model-neo4j-enterprise-edition]`
- **The critical mitigation: Topos uses GDS as a *test oracle only*, never as a runtime
  dependency.** A test-only GPLv3 dependency cannot copyleft the production binary — the GDS
  reference lives in a separate `Topos.Tests.GdsOracle` test project, and Topos's production
  assembly has no GDS reference. This is the standard pattern for using copyleft tools in
  permissively-licensed projects (cf. how many Apache-licensed projects test against GPL
  databases). **🔒 LOCKED: GDS is test-project-only; no GDS import in any non-test Topos code.**
- **Algorithm tier — 🟡 needs verification (Q9).** Official docs confirm GDS Enterprise
  requires a license key and adds Apache Arrow, but the *per-algorithm* Community/Enterprise
  split is not cleanly documented in the pages I could fetch. The listed algorithms (Louvain,
  Label Propagation, WCC, SCC, Triangle Counting, Local Clustering Coefficient) are *believed*
  to be in Community based on secondary sources, but this is **not verified from primary
  source.** If any are Enterprise-only, the oracle plan has a gap for those algorithms (M6
  analytics verification would need an alternative oracle for them).
  `[unverified:web — algorithm-tier claim needs primary-source confirmation]`
  **Action:** verify the per-algorithm tier against `neo4j.com/docs/graph-data-science/`
  before M6. Does not block M0–M1 (BFS/DFS/shortest-path/cycle/SCC are core graph algorithms
  with many free oracles — including a second independent one like igraph/GraphBLAS if needed).

**Net:** §5's claim "no new infrastructure" holds (GDS plugin is the only addition). But the
claim implicitly assumed "free" — the honest version is "free for test-oracle use under
GPLv3, isolated to the test project; algorithm-tier verification still owed." The §11 table
reflects this.

`[verified:docs=docs/DECISIONS.md §6]` `[verified:src=Rich-Learning-Base/src/RichLearning.V2/Memory/Neo4jGraphMemory.cs]`

---

## 6. Roadmap — M0 through M8 (🔒 structure LOCKED, details per-milestone)

Sequenced so each milestone is independently testable. Honest calibration against
"industrial-level C# hypergraph library": **months, not weeks.**
`[verified:docs=docs/BASE_INVESTIGATION.md §6]`

| M | Name | Scope | Exit criterion |
|---|---|---|---|
| **M0** | Storage kernel | `Handle`, `Vertex`, `Incidence`, `PropertyKey<T>`. Generational IDs (the `Generation` field ships from M0 so M4 compaction doesn't force a Handle layout change). Disable-flag tombstoning. The 2 invariants enforced. The §3.4 concurrency model (SWMR + lock-free counters + per-pool locks). **CSR frozen tiers are NOT in M0 — deferred to M4** (see below). | Thread-safe in-memory hypergraph with stable handles passing a fuzz+concurrency suite. **Plus measured benchmarks with two gates:** **(a) relative** — sparse-set pools (`SparseSet<T>` + per-pool `ReaderWriterLockSlim`) beat a naive `Dictionary<Handle, List<Handle>>` baseline at the project's scale (`[verified:src=docs/M0_BENCHMARK_RESULTS_2026-07-24.md §1]` — 2.2–2.4× faster for vertex/property access; within 15% of a *fair* two-direction thread-safe baseline for the incidence index at realistic scale); **(b) absolute** — per-hop traversal latency under an explicit budget derived from the RLB logistics workload's 3.7ms/step (~270Hz) figure `[verified:src=Rich-Learning-Base/INITIAL_README.md]`. **🔒 RESOLVED (2026-07-24, measured):** traversal measured at ~20ns/hop (`[verified:src=docs/M0_BENCHMARK_RESULTS_2026-07-24.md §4]`), negligible vs. 3.7ms — well inside any defensible graph-traversal share. The budget risk Q8 guarded against was not raw traversal cost but the O(N²) fan-in pathology on hub vertices, which the §3.4 redesign fixed (55ms → 337µs at N=8,000; `[verified:src=docs/M0_BENCHMARK_RESULTS_2026-07-24.md §5]`). Gate met in spirit and in fact. **CSR deferred to M4:** the benchmark's own conclusion — "nothing here argues for CSR/frozen tiers yet; the current `SparseSet` + `ReaderWriterLockSlim` design is within noise of a fair naive baseline and meaningfully faster for vertex/property" — made speculative CSR construction the over-engineering the project's discipline warns against. M4 (tiered memory + persistence) is the natural home; the `Generation` field is already in place so that deferral costs nothing. |
| **M1** | `IHypergraphQuery` + default algorithms | The 9-primitive interface + ~40 default-method algorithms (BFS/DFS, reachable, shortest-path, cycle, transitive closure, SCC). | Algorithm parity with yamafaktory/hypergraph. **Plus a GDS-parity test suite** (🔒 added per the §5 strategy): every algorithm passes a property-based test vs. Neo4j GDS. |
| **M2** | Reification + roles | `Role:Edge` vertices; `IncidenceRole` on incidences; the asserted/quoted/hypothesized mode flag (from RDF 1.2). | Recursive hypergraph works; nested reification depth-N round-trips. |
| **M3** | Properties + typed views | `PropertyKey<T>` registry; composable views (subgraph/mask/unmodifiable/union from JGraphT); set algebra (union/intersect/diff from HyperNetX, doubling as version-diff). | A real schema (nodes + relationships + provenance) expressible without ad hoc tables. |
| **M4** | Tiered memory + persistence (packaging split) | Hot LRU + cold LSM (yamafaktory's `PersistentHypergraph`); columnar on-disk (Kuzu's pattern). Assembly splits `.Core` + `.Persistence`. | Graphs larger than RAM work; hot-tier lookup is O(1). Reference: MIT-licensed Kuzu codebase (fork-and-study, not depend-on). |
| **M5** | The AI-memory layer (the differentiator) + **falsifiability gate** | Embeddings unified (`PropertyKey<float[]>` + ANN, from Kuzu's vector extension); learnable edges (DHG's injection point adapted to C#); provenance/versioning first-class; confidence-quality tracking. **Plus the non-RLB second consumer** (toy chat-agent memory demo). | An agent memory runs on Topos at scale. **The second consumer is the falsifiable test of domain-agnosticity** — if the kernel can't serve a consumer it wasn't designed around, the "standalone library" claim isn't yet true. |
| **M6** | Analytics | s-walk traversal, community detection, modularity. | Strong GDS verification path (GDS ships Louvain/LabelProp/WCC/SCC/TriangleCount/ClusteringCoef as direct oracles). |
| **M7** | Spectral (deferred) | Laplacian, incidence matrix, partitioning hooks. | Only if a domain forces it. Three voices agree to defer (investigation + both reviewers). |
| **M8** | Polish + docs + NuGet + OSS | API stability, benchmark suite, HIF interchange support (port from Julia/HyperNetX), docs site. | OSS-ready. |

### 6.1 The build-as-RLB-kernel implications (🔒 LOCKED)

Because Topos is built as RLB's kernel first (`DECISIONS.md §6`):
- **RLB becomes a `ProjectReference`** in `tests/RichLearning.V2.Tests/RichLearning.V2.Tests.csproj`
  during M0–M4. RLB's **337-test V2 suite** (verified: exactly 337 `[Fact]`/`[Theory]`) becomes
  the first real consumer. `[verified:src=Rich-Learning.V2.csproj — net10.0, only dep Neo4j.Driver]`
  `[verified:src=tests/RichLearning.V2.Tests — 337 test attributes]`
- **Dependency direction preserved:** Topos references nothing upstream (no RLB types leak into
  Topos); RLB references Topos. The kernel stays clean.
- **The M5 second consumer must be non-RLB** (toy chat-agent memory demo). This is the
  falsifiable test of domain-agnosticity — the point of the build-as-RLB-kernel strategy.
- **RLB is touched during M0–M4.** The earlier "RLB stays untouched" framing is superseded.

### 6.2 The Rust-removal alignment (context, not Topos scope)

RLB's active task (`ToDoList/Hypergraph-Port-From-Petgraph.md`) is removing the Rust petgraph
kernel and replacing it with C# over V2's existing hyperedge substrate. **Topos is the
eventual home for that C# substrate** — but Topos M0 starts clean, not as a port of RLB's
`HyperEdge.cs`. The RLB model is the *requirements source* (§1, §3.1), not the code to fork.
`[verified:docs=Rich-Learning-Base/ToDoList/Hypergraph-Port-From-Petgraph.md]`

---

## 7. AI-memory design patterns (🔒 LOCKED — the greenfield layer)

These are the patterns Topos builds (not borrows), validated by the source surveys. Full
provenance in `BASE_INVESTIGATION.md §5.5`; competitor validation in
`AGENT_MEMORY_COMPETITORS.md §7`.

| # | Pattern | Provenance | Competitor validation |
|---|---|---|---|
| 12 | Reification via `Role:Edge` vertex | RDF 1.2 §1.5, TypeDB | All 4 competitors ✗ at model layer — the gap |
| 13 | Asserted/quoted/hypothesized mode flag | RDF 1.2 unasserted triple term | (novel for agent memory) |
| 14 | Incidence-level (cell) properties | HyperNetX MultiIndex | All 4 competitors ✗ — only node/edge bags |
| 15 | Three metadata slots (content+provenance+weight) | SimpleHypergraphs.jl | (validates the slot count) |
| 16 | Hyperedge groups / tiers | DHG | Graphiti partial (structural tiering) |
| 17 | s-walk / s-distance traversal | HyperNetX/Julia | (hypergraph-specific; GDS can't verify) |
| 18 | Community detection | SimpleHypergraphs.jl | GDS ships direct oracles (M6) |
| 19 | Differentiable weight-injection point | DHG `v2e_aggregation` | Cognee partial (`Edge.weights` slot, no learner) |
| 20 | Tiered LSM+LRU | yamafaktory `PersistentHypergraph` | Letta ✓ (core/recall/archival — different mechanism) |

`[verified:docs=docs/BASE_INVESTIGATION.md §5.5]` `[verified:docs=docs/AGENT_MEMORY_COMPETITORS.md §4, §7]`

---

## 8. Honest rejects (🔒 LOCKED)

Patterns investigated and explicitly rejected, with reasons. `[verified:docs=docs/BASE_INVESTIGATION.md §5.6]`

- **pandas backing** (HyperNetX) — wrong performance tier.
- **`Into<usize>` edge bound** (Rust crate) — too restrictive; separate cost from payload.
- **swap-with-last removal** (Julia) — silently invalidates handles; disqualifying.
- **structural-equality edge merging** (DHG) — can't have distinct provenance-edges over the same tuple.
- **the partitioner itself** (KaHyPar) — overkill; undirected-only; the storage spine is the value.
- **spectral machinery** (Laplacian, dense incidence, KaHyPar partitioning) — YAGNI for
  symbolic/traversal workload; add only if a domain forces it.

---

## 9. What the competitors independently validated (🔒 LOCKED)

From `AGENT_MEMORY_COMPETITORS.md §7` — design choices the incumbents arrived at independently,
strengthening Topos's:

1. **Content-addressed identity** (Cognee's `uuid5(ClassName:joined)`) — the strongest identity
   model among incumbents; matches Topos's stable-handles + newtype contract. Better than
   Graphiti's non-deterministic uuid4. `[verified:src=topoteretes/cognee DataPoint.py]`
2. **Unified embeddings on the symbolic record** (Graphiti's `fact_embedding` on edges; Letta's
   pgvector column on the passage row) — validates `DECISIONS.md` Q5 (embeddings as
   `PropertyKey<float[]>`, not a parallel store). `[verified:src=getzep/graphiti edges.py]`
3. **The three recurring failure modes** (n-ary fragmentation, no cell properties, no
   reified facts-as-entities) map exactly onto three Topos primitives — direct validation of
   the contract. `[verified:docs=docs/AGENT_MEMORY_COMPETITORS.md §5.3]`

---

## 10. Integrity, caveats, and corrections

### 10.1 Standard caveats

- **No code, no tests, no benchmarks yet.** All performance claims are inferred from storage
  representation. M0's exit criterion requires *measured* benchmarks.
- **Source-grade, not formally-verified.** "Is this a good design" is not decidable the way a
  proof-checker decides validity. `[unverified:inferred]` tags mark reasoning, not reading.
- **Bus-factor risk** in borrowed *patterns*' sources (yamafaktory single-maintainer;
  SimpleHypergraphs.jl small team). Borrow patterns, not upstream code.

### 10.2 Overclaims caught and corrected during review

The competitor survey's first draft overclaimed "no production-grade n-ary DB exists" — refuted
by TypeDB (genuinely n-ary, production-grade). Corrected to the defensible intersection form:
no n-ary DB satisfies *embedded + Cypher + permissive + ecosystem* simultaneously.
`[verified:docs=docs/AGENT_MEMORY_COMPETITORS.md §5.4–§5.5]` Reviewers should treat this as the
template for catching similar overclaims in *this* spec.

### 10.3 The Topos defensibility is intersection-specific

§5.5 of the competitor survey reframes Topos's defensibility: it rests on being *the first n-ary
substrate at the embedded/Cypher/ecosystem intersection*, not on n-ary being impossible in
general (TypeDB proves it's possible server-side). **The C# niche is itself unoccupied** — none
of the four competitors, nor TypeDB/TigerGraph, ships a C# memory substrate. This is the gap
`BASE_INVESTIGATION.md` established independently and RLB's `Hypergraph-Port-From-Petgraph.md`
confirms from the consumer side. `[verified:docs=docs/AGENT_MEMORY_COMPETITORS.md §5.5]`

### 10.4 The Medium/Kuzu source still unread

The Medium piece "I Analyzed 163K Lines of Kuzu's Codebase — Here's Why Apple Wanted It"
returned HTTP 403 to fetch. It may contain deeper architectural detail on Kuzu's storage engine
than the file-level reading in `BASE_INVESTIGATION.md §3.8`. **Recommended reading before M4.**
`[verified:docs=docs/SESSION_HANDOFF.md §7]`

---

## 11. Decisions summary (quick reference)

| Decision | Status | Source |
|---|---|---|
| Build as RLB kernel first; M5 = non-RLB second consumer | 🔒 LOCKED | `DECISIONS.md §6` |
| Neo4j GDS as correctness oracle for standard algorithms | 🔒 LOCKED | `DECISIONS.md §6` |
| GDS test-only isolation (GPLv3 cannot reach production binary) | 🔒 LOCKED | this doc §5.1 |
| 4 primitives + 2 invariants | 🔒 LOCKED | `BASE_INVESTIGATION §5.1`, `DECISIONS.md §1` |
| 3-layer substrate (Knowledge / Graph / Storage) | 🔒 LOCKED | this doc §4.1 |
| Algorithms as cross-cutting capabilities, not a top layer | 🔒 LOCKED (shape) | this doc §4.2 |
| Cardinality/validation rules live in layer 1 (Knowledge model), not the kernel | 🔒 LOCKED | this doc §4.1 |
| Concurrency model: SWMR + lock-free counters + per-pool `ReaderWriterLockSlim`s | 🔒 LOCKED (amended 2026-07-24) | this doc §3.4 |
| M0 benchmark gate = relative (beats naive) **AND** absolute (per-hop vs. 270Hz) — **BOTH MET** | 🔒 LOCKED (met 2026-07-24) | this doc §6 M0 |
| CSR frozen tiers deferred from M0 to M4 (benchmark-unforced) | 🔒 LOCKED (2026-07-24) | this doc §6 M0, `M0_BENCHMARK_RESULTS_2026-07-24.md` |
| Reserved hot-path slots; spectral deferred; packaging split at M4; no reification cap; embeddings as `PropertyKey<float[]>` | 🔒 LOCKED (5 Qs) | `DECISIONS.md §1` |
| M0 measured-benchmark gate; `float` not `double` | 🔒 LOCKED | `BASE_INVESTIGATION §8.1` |
| §1.2 empirical case anchored on Condition-aggregation (not the r[3] flag) | 🔒 LOCKED | this doc §1.2 |
| Apple/Kuzu = weak-positive signal only, not thesis validation | 🔒 LOCKED | `BASE_INVESTIGATION §1, §3.8` |
| Identity: hypergraph library M0–M3; incidence-model + memory-substrate reframes are horizon, not founding | 🔒 LOCKED (lean) | this doc §2.3–§2.4 |
| Identity: hypergraph library M0–M3; incidence-model reframe is a reach goal | 🟡 OPEN (Q2) | `DECISIONS.md §4.1` |
| M5 sequencing: split it (shapes early, machinery M5) | 🟡 OPEN (Q3) | `DECISIONS.md §2.1` |
| "Paradox-compression" artifact citation | 🟡 OPEN (Q1, for Nasser) | this doc §12 |
| Handle generation-bits: include from M0 (a) or add at M4 (b)? | 🟡 OPEN (Q7) | this doc §3.5 |
| Per-hop latency budget derivation from 270Hz | 🔒 RESOLVED (Q8, 2026-07-24) — measured ~20ns/hop, negligible vs. 3.7ms; real budget risk (fan-in) fixed by §3.4 redesign | this doc §6 M0 |
| GDS per-algorithm Community/Enterprise tier verification | 🟡 OPEN (Q9) | this doc §5.1 |
| Capability split (Traversal/Analytics/Learning/AI-services/Projection) — confirm exact partition | 🟡 OPEN (Q10) | this doc §4.2 |

---

## 12. Open questions for this review (🟡 — your input here)

> **For GPT and Claude:** these are the live decisions. Everything else is locked or
> informational. Please adjudicate each, or confirm the lean.

**Q1 (for Nasser, but reviewers may weigh in) — the "paradox-compression" citation.**
The handoff referenced a "paradox-compression finding" as the empirical spec opener, but a
full grep of RLB finds no artifact by that name. This spec uses the verified
deferred-HyperEdge / synthesis-break evidence (§1.2) instead. Is "paradox-compression" a real
artifact we should cite, or was it a paraphrase? *If real, point us at it; if paraphrase, §1.2
stands as the opener.*

**Q2 — the identity fork (hypergraph vs. incidence model).**
`DECISIONS.md §4.1` leans: build M0–M3 as a hypergraph library, hold the incidence-model
reframe as a reach goal. Confirm? Or should the spec commit to the incidence-model framing from
the start (bigger ambition, less evidence)? *The primitive shape doesn't preclude either; the
question is whether to lock the identity before the implementation exists.*

**Q3 — M5 sequencing (split it, or defer all).**
GPT: defer all of embeddings/learnable/provenance to M5. Fable: split it — primitive shapes in
M0/M2, machinery in M5. The lean is Fable (the Incidence fields are already justified by these
concerns; embeddings as `PropertyKey<float[]>` from day one costs nothing). Confirm "split it"?
*This affects what lands in M0 — see §6.*

**Q4 — contract seams (consistency check).**
The contract (§3), the layer architecture (§4), and the roadmap (§6) were written from
different source docs. Do they compose cleanly in your read? *The §4 revision (substrate +
cross-cutting capabilities) was applied to resolve one such seam; a second pass on the rest
would help.*

**Q5 — RLB coupling depth during M0–M4.**
Topos becomes an RLB `ProjectReference` and RLB's 337 tests become the first consumer. How deep
should the coupling go? Options: (a) Topos exposes primitives; RLB's `HyperEdge.cs` is rewritten
to sit on Topos and the 337 tests run against the new substrate; (b) Topos exposes primitives;
RLB keeps its `HyperEdge.cs` and only the algorithm layer (BFS/cycle/shortest-path from RLB's
Rust-port todo) moves to Topos. *(a) is deeper validation but more RLB churn; (b) is safer but
validates less.* Lean?

**Q6 — anything we overclaimed in *this* spec?**
We caught and corrected one overclaim in the competitor survey (§10.2). Please scan this spec
for similar gaps between claim and evidence — especially §1 (the empirical case), §3.1 (the
RLB-to-contract mapping), and §9 (the independent-validation claims).

> **Claude's first review (saved verbatim at `docs/reactions/03_Claude_specification-review.md`)
> surfaced exactly this kind of gap — and the spec has been revised to address all 7 points.
> Summary of what Claude's review changed:**
> - **#1 (resolved, §1.2):** re-anchored the empirical case on Condition-aggregation semantics
>   (the structural non-derivability), demoted the `r[3]` flag to corroborating code evidence.
>   The strongest argument is no longer a patchable implementation quirk.
> - **#2 (→ Q7):** Handle generation-bits redundancy — opened as Q7 with the
>   logical-vs-physical-persistence resolution stated.
> - **#3 (resolved, §3.4):** added a locked concurrency model (SWMR + lock-free counters +
>   per-pool locks) so M0 isn't discovering this retroactively. *(Original text specified
>   "COW pages" for the mutable tier — corrected 2026-07-24 after M0 benchmarking showed COW
>   was O(N²) on hub vertices; see the 🔧 correction block in §3.4. The SWMR + per-pool-lock
>   shape stands; the COW mechanism within it does not.)*
> - **#4 (→ Q8):** M0 benchmark gate now requires an absolute per-hop budget (from the 270Hz
>   figure), not just "beats naive." Exact budget derivation is Q8.
> - **#5 (resolved, §4):** cardinality validation explicitly lives in layer 1 (Knowledge
>   model), resolving the §3.2 Q4 "store doesn't judge" tension and clarifying the RLB-port seam.
> - **#6 (→ Q9):** GDS licensing investigated — Community is GPLv3, mitigated by test-only
>   isolation (locked); per-algorithm tier verification owed as Q9.
> - **#7 (resolved, §2.2):** added the one-sentence scope clarification on belief-revision.
>
> The three Claude flagged as pre-contract-lock (#1, #3, #5) are resolved. Q7/Q8/Q9 below are
> the residual open items from that review.

**Q7 — Handle generation-bits: include from M0, or add at M4?** *(From Claude #2.)*
Invariant 1 (logical persistence) + monotonic-never-reused counter leaves no M0 scenario for
generation bits to disambiguate — but M4 compaction (physical slot relocation) is exactly that
scenario. Option (a): include from M0 (Handle struct stable through M4, costs a few bits);
option (b): add at M4 (leaner M0, but a Handle layout change mid-roadmap breaks persisted
formats). *Lean: (a) — Handle layout stability is worth a few bits. But this is a
storage-layout decision with perf implications, so it's Nasser's call.*

**Q8 — derive the per-hop latency budget from the 270Hz figure.** *(From Claude #4.)*
**🔒 RESOLVED (2026-07-24, during M0 implementation).** The 270Hz (3.7ms/step) is the
*whole-step* budget; graph traversal is one share. Rather than derive an abstract fraction,
M0 measured the actual quantity: ~20ns/hop for a 5-hop chain walk
(`[verified:src=docs/M0_BENCHMARK_RESULTS_2026-07-24.md §4]`) — i.e., ~1µs for a full decision's
worth of traversal, ~0.03% of the 3.7ms step. Well inside any defensible share without needing to
argue the split. The budget risk Q8 was *actually* guarding against turned out not to be raw
traversal cost but the O(N²) fan-in pathology on hub vertices (55ms to build one 8,000-member
hyperedge membership set) — and that was fixed by the §3.4 redesign (down to 337µs). So the gate
"the kernel must not consume the step" is met both in the trivial dimension (traversal latency)
and in the dimension that actually mattered (fan-in). **Gate closed.**

**Q9 — verify GDS per-algorithm Community/Enterprise tier.** *(From Claude #6.)*
§5.1 locks test-only isolation (handles the GPLv3 concern). But if any of Louvain/LabelProp/
WCC/SCC/TriangleCount/LocalClusteringCoef are Enterprise-only, the M6 oracle plan has a gap
for those algorithms. *Needs primary-source verification against
`neo4j.com/docs/graph-data-science/` before M6. Does not block M0–M1.*

**Q10 — confirm the capability partition.** §4.2 splits what was a single "Algorithms" layer
into five cross-cutting capabilities (Traversal / Analytics / Learning / AI services /
Projection). *Does this partition land right, or should any be merged/split? Specifically: is
"Projection" distinct enough from "Analytics" to deserve its own row, and does "AI services"
belong as a capability or as a layer-4 stratum? The three-layer substrate is locked either way;
only the above-substrate organization is open.*

---

## 13. Sources (for the reviewers' audit)

**Topos investigation docs (the inputs to this spec):**
- `docs/BASE_INVESTIGATION.md` — 10-library survey + proposed contract + roadmap skeleton
- `docs/AGENT_MEMORY_COMPETITORS.md` — Zep/mem0/Letta/Cognee survey + n-ary-DB matrix
- `docs/DECISIONS.md` — two-reviewer synthesis; decision log
- `docs/reactions/01_GPT_first-reaction.md`, `02_Fable_first-reaction.md` — verbatim reviews

**RLB evidence (the empirical grounding for §1 and §3.1):**
- `src/RichLearning.V2/Models/HyperEdge.cs` — the n-ary role-tagged hyperedge (247 lines)
- `tests/RichLearning.V2.Tests/HyperedgeTests.cs` — 28 tests against it
- `tests/RichLearning.V2.Tests/RichLearning.V2.Tests.csproj` — net10.0, Neo4j.Driver only
- `tests/RichLearning.V2.Tests/` — 337 `[Fact]`/`[Theory]` total
- `INITIAL_README.md` — five-domain measured results
- `ToDoList/Hypergraph-Port-From-Petgraph.md` — the active Rust-removal task; Topos's
  requirements source
- `src/RichLearning.V2/Memory/Neo4jGraphMemory.cs` — confirms Neo4j already in stack (§5)

**Library provenance (for the contract, §3):**
- yamafaktory/hypergraph (`indexes.rs`, `query/trait_def.rs`, `disk/types.rs`)
- EnTT (`sparse_set.hpp`, `entity.hpp`)
- HyperNetX (`property_store.py`, `incidence_store.py`)
- RDF 1.2 (§1.5), TypeDB (docs.vaticle.com), Kuzu (`src/storage/table/`)

**Competitor + DB provenance (for §5, §7, §9):**
- getzep/graphiti (`edges.py`, `prompts/extract_edges.py`, arXiv 2501.13956)
- mem0ai/mem0 (commit `a488e19044e4`, `configs/base.py`)
- letta-ai/letta (`schemas/memory.py`, `schemas/passage.py`)
- topoteretes/cognee (`DataPoint.py`, `CogneeGraphElements.py`)
- TypeDB (typedb.com/docs; LICENSE = MPL-2.0), TigerGraph (gsql-ref §4.2)
- Neo4j GDS licensing: `neo4j.com/blog/news/open-core-licensing-model-neo4j-enterprise-edition`
  (Community = GPLv3, Enterprise = commercial/AGPLv3); `neo4j.com/docs/graph-data-science/current/installation/`
  (Enterprise requires license key, adds Apache Arrow)

---

## Appendix A — Design history (process provenance)

> The body of this specification is written to be timeless — it states *what* and *why*, not
> *who said what when*. This appendix preserves the reviewer-process provenance that an earlier
> draft carried inline. It exists for traceability and for anyone who wants to understand how
> the spec arrived at its current shape. The authoritative record of the debate is the
> `docs/reactions/` folder (verbatim reviews) and `docs/DECISIONS.md` (the synthesis).

**Review rounds applied to this specification:**

1. **Claude's review** (`docs/reactions/03_Claude_specification-review.md`) — 7 issues raised.
   Resolved into the spec: §1.2 re-anchored on Condition-aggregation (was over-anchored on the
   `r[3]` code discriminator — a patchable implementation quirk, not a theorem); §3.4
   concurrency model added (was a missing M0 decision); §4.1 cardinality-placement stated (was
   an unstated seam); §2.2 belief-revision scope clarified. Opened: Q7 (Handle generation-bits
   redundancy), Q8 (per-hop latency budget), Q9 (GDS algorithm-tier verification). §5.1 GDS
   licensing investigated and locked (test-only isolation).

2. **GPT's review** (`docs/reactions/04_GPT_specification-review.md`) — approved for
   implementation; 5 requested changes. Applied: §1 compressed ~40% with the 3-line thesis
   leading; reviewer-attribution removed from the body (moved here); "AI age"/"AI-memory niche"
   reworded to workload descriptors; §4 top "Algorithms" layer split into cross-cutting
   capabilities (Traversal/Analytics/Learning/AI-services/Projection) — Q10 opened on the exact
   partition; §2.4 added the "persistent symbolic memory substrate" as explicit horizon (not a
   contract restructure), per GPT's own "don't change the architecture today" caveat.

**Earlier review rounds (on `BASE_INVESTIGATION.md`, the spec's input — see
`docs/reactions/01_GPT_first-reaction.md`, `02_Fable_first-reaction.md`):**

3. **GPT's first review** — proposed the 5-layer architecture (now revised in §4 to a 3-layer
   substrate + capabilities) and the "typed incidence model" reframe (now horizon in §2.4).
4. **Fable's first review** — proposed the build-as-RLB-kernel strategy (adopted, `DECISIONS.md
   §6`), the `float`-not-`double` fix, the measured-benchmark M0 gate, and the "split M5"
   sequencing lean (Q3).

**The integrity discipline.** Throughout, every claim carries an *evidence* provenance tag
(`[verified:src=path]`, `[verified:spec=§]`, `[verified:web=url]`, `[unverified:inferred]`) —
this is about a claim's *truth*, and stays in the body. The *process* provenance ("reviewer X
said Y") lives only in this appendix and in `docs/reactions/` + `docs/DECISIONS.md`.

---

*End of specification. Both reviewers (GPT, Claude) have approved for implementation. The
remaining `🟡 OPEN` questions (Q1–Q3, Q7–Q10) are adjudication items, not blockers — Q1 (the
"paradox-compression" citation) and Q7 (Handle generation-bits) are the two that most affect
what M0 code looks like and should be resolved before M0 implementation starts.*
