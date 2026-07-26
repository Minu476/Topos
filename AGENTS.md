# AGENTS.md — Topos

Guidance for any agent (ZCode/GLM, Claude Code, Cursor, Continue, Kimi, Antigravity/Gemini)
working in this repository. **Read this first on every session.**

---

## 1. What Topos is

**Topos** is a standalone, domain-agnostic **typed-property hypergraph library for C#**,
purpose-fit for AI / agent memory — LLM reasoning, explainability, learnable edges, tiered
memory, and provenance.

> *Topos* (Greek τόπος, "place / location") is the root of *topology*. The name invokes the
> central thesis: **knowledge stored as topological graph structure rather than neural-network
> weights.** In category theory, a *topos* is a deep theory of structured contexts — a fitting
> ambition for a foundational substrate that many domains build on.

**Current status (2026-07-25): M0–M6 implemented and tested; M7 (spectral) deferred by design;
M8's API-stability scope is done.** The spec was approved (GPT + Claude passes,
`docs/reactions/`) before implementation started. Topos is a live `ProjectReference` in
Rich-Learning-Base's V2 codebase (`ToposGraphProjection.cs`) and has a second real consumer
(NexusVerifier's AND-OR proof-search chainer) — see `docs/SESSION_HANDOFF.md` for the precise
carry-on state and `docs/DECISIONS.md`'s three 2026-07-25 entries for what M8 resolved and why
it's closed. **M8's other spec items — HIF interchange, a docs site, and NuGet-publish readiness
— are deliberately deferred**, each gated on a condition that hasn't arrived (a forcing consumer,
or a decision to go public); they are not part of what "M8 done" means here.

**M9 is now scoped (2026-07-25), not started.** A new `Topos.Hypergraph.Knowledge` package —
layer-1 role-aware directed traversal (`DirectedBfs`/`DirectedShortestPath`/`RoleFilteredMembers`
over `IHypergraphQuery`), generalizing a pattern three independent real consumers already
hand-rolled (ChatMemory, RLB's own `ToposGraphProjection`, NexusVerifier). See
`docs/SPECIFICATION.md` §6's M9 row and `docs/DECISIONS.md`'s "M9 SCOPED" entry for the full
scope, exit criterion, and why the forcing evidence is unusually strong. No code exists yet —
don't start implementing without checking with Nasser first.

The namespace is `Topos.Hypergraph`, locked for M0–M3 per `docs/DECISIONS.md` §4.1 — the deeper
"incidence model" reframe stays a reach goal, not adopted.

---

## 2. The thesis Topos exists to serve (and the harder question, now answered)

The age of AI **changed the optimization criteria** for a graph library — it did not invent a
new kind of graph. Historical graph libraries optimized for shortest paths, centrality,
partitioning, spectral decomposition, community detection. The AI-memory workload optimizes
for incremental updates, provenance, explainability, retrieval, symbolic+vector coexistence,
stable identities, partial activation, long-lived mutable knowledge. These are genuinely
different workloads, and no hypergraph library in any language is built for the second set.

**The harder question, now answered** (`docs/AGENT_MEMORY_COMPETITORS.md`, closed): the systems
actually competing for the "AI agent memory substrate" niche (Zep/Graphiti, mem0, Letta,
Cognee) are *not* hypergraph libraries — they chose binary property graphs or temporal binary
graphs. The survey found the field never seriously tried hypergraphs and rejected them (zero
evidence either way in any of the four codebases); binary is good enough for the dominant 80%
of workloads, but costs real expressiveness in exactly three places (n-ary facts, cell-level
properties, reified facts-as-entities) that map onto Topos's primitives. See
`docs/AGENT_MEMORY_COMPETITORS.md` §7 for the full answer. (The "paradox-compression" empirical
counter-argument referenced in earlier handoffs turned out to be from an unrelated project — see
`docs/PARADOX_COMPRESSION_SEARCH.md`; the spec's §1.2 Condition-aggregation argument stands on
its own instead.)

---

## 3. The storage contract (implemented, M0–M6)

The four-primitive + two-invariant shape, synthesized from source-level reading of 10
libraries, survived every attempt to add a fifth primitive during spec review — and is now
built and tested (`src/Topos.Hypergraph/`). The concurrency model was benchmark-corrected
during M0 implementation (copy-on-write → `ReaderWriterLockSlim`-per-pool, after COW measured
O(N²) on hub vertices) — see `docs/M0_BENCHMARK_RESULTS_2026-07-24.md` for the full data and
`docs/DECISIONS.md` for what stayed locked vs. what changed.

```
PRIMITIVES (4):
    Handle        — newtype + monotonic never-reused counter + generational version bits
    Vertex        — Handle + VertexRoles (bitmask) + VertexStatus (reserved hot-path slot)
                    + PropertyBag (columnar, EnTT-style sparse-set pools)
    Incidence     — SourceHandle + MemberHandle + IncidenceRole (byte) + Ordinal (packed struct;
                    cell properties attach here)
    PropertyKey<T>— identity (string) separate from PropertyId (int, per-process registry)

INVARIANTS (2):
    1. Dormant never garbage-collected; provenance edges always resolve (even to dormant targets).
    2. VertexRoles and IncidenceRole are independent axes.
```

Read `docs/BASE_INVESTIGATION.md` §5 for the full proposal and the patterns each primitive
borrows from (Rust crate / EnTT / HyperNetX / TypeDB / RDF 1.2). Read `docs/DECISIONS.md` for
which parts of the contract the two reviewers locked vs. left open.

---

## 4. Critical relationships (build-as-RLB-kernel decision, now live)

**Decision (Nasser, 2026-07-23): build Topos as RLB's kernel first**, with standalone-library
ambition as a falsifiable M5 milestone (a non-RLB second consumer). See `docs/DECISIONS.md` §6.
Both halves of this are done, not just planned:

- **Rich-Learning-Base** (`~/Projects/Rich-Learning-Base`) — **first consumer, live.** Topos
  is a `ProjectReference` in RLB's V2 csproj — see
  `src/RichLearning.V2/Learning/ToposGraphProjection.cs`, which does hyperedge-aware
  pathfinding over a Topos `HypergraphKernel` built from RLB's landmarks/transitions/
  hyperedges. RLB's 346-test suite, including live Neo4j round-trips, passes against it. A
  small standalone demo also exists in RLB: `tools/ToposHyperedgeDemo` (`dotnet run --project
  tools/ToposHyperedgeDemo`) — side-by-side comparison of hyperedge-blind vs. Topos-backed
  pathfinding, useful for a quick hands-on check that the integration actually does something.
- **FSDE** (`~/Projects/FSDE`) — still decoupled. May adopt Topos later.
- **Dependency direction preserved:** Topos still references nothing upstream (no RLB types
  leak into Topos); RLB references Topos. The kernel stays clean.
- **M5 exit criterion — done.** The non-RLB second consumer is `samples/Topos.Samples.ChatMemory`
  in this repo. The kernel served a consumer it wasn't designed around; the domain-agnosticity
  claim has a falsifiable test behind it now, not just an intention.

---

## 5. At session start — pull your briefing

**On launch, call these in order before doing anything else** (when the FSDE MCP tools are
loaded; if not, use the `fsde` CLI as a fallback):

1. **`fsde_start_session`** with `projectPath=/Users/nassertowfigh/Projects/Topos`. Returns
   the session briefing.
2. **`fsde_read_directives`** (recipient=agent, status=open) — pick up directives.
3. **`fsde_get_todos`** — the live todo list. The markdown files in `docs/` are the
   human-readable source of truth; Neo4j catches up via re-ingestion.

If FSDE MCP tools are NOT loaded in the session, the `fsde` CLI works:
`fsde get-context --dir /Users/nassertowfigh/Projects/Topos`.

**Either way, always read `docs/SESSION_HANDOFF.md` first** — it carries the carry-on state
across sessions and is authoritative when FSDE is cold.

---

## 6. Before design/code claims — ground in source

- **`fsde_find_concept`** — when a task touches a named concept (reification, fossilization,
  incidence, role semantics, tiered memory), look it up.
- **`fsde_context_for_code`** — before changing or proposing a file/method, pull its context.
  Hint, not oracle — verify against the real file.
- **For library claims:** the investigation's integrity standard is source-grade. Every claim
  is tagged `[verified:src=path]`, `[verified:spec=section]`, `[verified:paper=ref]`,
  `[verified:web=url]`, or `[unverified:inferred]`. **Continue this discipline.** No unsourced
  assertions. This mirrors NexusVerifier's `SOLVED_PROBLEMS.md` (verified vs. axiom-scaffolded
  vs. `sorry`).

---

## 7. At session end — write back so the next session inherits

- **`fsde_end_session`** with a summary of what was done, decisions made, what's next.
- **`fsde_log_work_event`** for anything the next session needs to know.
- **`fsde_set_objective`** if the goal shifted.
- **Update `docs/SESSION_HANDOFF.md`** so the next session opens briefed, not cold.

---

## 8. The repository layout (2026-07-24 — M0–M6 implemented)

```
~/Projects/Topos/                     # github.com/Minu476/Topos (private)
├── README.md                         # public-facing intro — kept in sync with actual status
├── AGENTS.md                         # this file
├── Topos.sln
├── src/
│   ├── Topos.Hypergraph/             # the kernel: Handle, Vertex, Incidence, PropertyKey<T>,
│   │                                  # IHypergraphQuery, traversal, reification, views/set
│   │                                  # algebra, embeddings/learnable edges/provenance,
│   │                                  # s-walk/label-propagation/triangle-count/modularity
│   └── Topos.Hypergraph.Persistence/ # tiered LRU+snapshot persistence (packaging split at M4)
├── tests/
│   ├── Topos.Hypergraph.Tests/           # kernel unit + fuzz + concurrency + stress tests
│   ├── Topos.Hypergraph.Persistence.Tests/
│   └── Topos.Tests.GdsOracle/             # Neo4j GDS-parity oracle — see docs/GDS_ORACLE_SETUP.md
├── samples/
│   └── Topos.Samples.ChatMemory(.Tests)/  # M5's non-RLB second consumer (falsifiability gate)
├── benchmarks/
│   └── Topos.Hypergraph.Benchmarks/  # BenchmarkDotNet suite — see docs/M0_BENCHMARK_RESULTS_*.md
└── docs/
    ├── BASE_INVESTIGATION.md         # the 10-library source-verified analysis + contract
    ├── AGENT_MEMORY_COMPETITORS.md   # the Zep/mem0/Letta/Cognee survey + n-ary-DB matrix
    ├── SPECIFICATION.md              # the consolidated spec — approved by GPT + Claude
    ├── DECISIONS.md                  # what reviewers locked vs. left open
    ├── SESSION_HANDOFF.md            # carry-on context — READ FIRST
    ├── M0_BENCHMARK_RESULTS_2026-07-24.md  # measured benchmark gate + the COW→RWLS correction
    ├── GDS_ORACLE_SETUP.md           # GDS-parity Docker setup + the Neo4j credential-isolation
    │                                  # writeup (host Neo4j instance rebind/rotation, unrelated
    │                                  # to Topos itself but relevant if you touch that instance)
    ├── PARADOX_COMPRESSION_SEARCH.md # resolves spec §12 Q1 — the citation was from an unrelated project
    ├── GDS_ALGORITHM_TIERS.md        # resolves spec §12 Q9 — GDS Community/Enterprise verification
    ├── NEXUS_VERIFIER_INTEGRATION_FINDINGS.md  # M8/M9 input: findings from the NexusVerifier
    │                                  # integration (the second real Topos consumer). Read before
    │                                  # the M8 API-stability review — 6 source-cited findings.
    ├── ROLE_CONVENTIONS.md           # M8: the documented byte-backed-enum role pattern
    │                                  # (resolves finding #3) — no kernel change, consumer guidance
    └── reactions/                    # verbatim GPT/Claude review rounds
```

**M0–M6 are implemented and tested.** **M7 is deferred by design** (spectral
machinery — no domain forces it yet). **M8's API-stability scope is done** (two passes: the
NexusVerifier-findings freeze decisions, then a broader public-surface audit); HIF interchange,
docs site, and NuGet-publish readiness are separately deferred, not blockers. **M9 is scoped but
not started** (layer-1 role-aware directed traversal, `docs/SPECIFICATION.md` §6) — see
`docs/SESSION_HANDOFF.md` §4.1 and `docs/DECISIONS.md`'s four 2026-07-25 entries.

---

## 9. Verification strategy — Neo4j GDS as the correctness oracle

**Decision (Nasser, 2026-07-23): use Neo4j GDS (Graph Data Science library) as an independent
oracle for Topos's algorithms.** This is the Lean-for-NexusVerifier pattern applied to graph
algorithms: a trusted external implementation as ground truth. See `docs/DECISIONS.md` §6.

What this means concretely:
- For each standard algorithm Topos implements (BFS/DFS/shortest-path/cycle/SCC/PageRank/
  community detection), a paired test runs the same query against the same graph in Neo4j
  GDS and asserts the outputs match.
- Property-graph projection of a hypergraph (via the ProjectionEngine from
  BASE_INVESTIGATION §5.4) gives a binary-graph view GDS can operate on — the standard
  algorithms verify cleanly through this projection.
- For algorithms where the hypergraph and its projection disagree (s-walk, role-gated
  reachability, anchor/condition/target semantics), GDS can't verify — Topos's answer is
  the novel claim there. GDS is the oracle for the *standard* algorithms only.
- **M1's exit criterion now includes a GDS-parity test suite.** M6 (analytics — Louvain,
  Label Propagation, WCC/SCC, Triangle Counting, Local Clustering Coefficient) gets a strong
  verification path: GDS ships all of these as direct oracles.
- Neo4j is already in Nasser's stack (RLB's `Neo4jGraphMemory`, FSDE). No new infrastructure
  — the GDS plugin install is the only addition. Test harness: a `Topos.Tests.GdsOracle`
  project/class added during M1.
- **This is implemented and running, not just planned.** `tests/Topos.Tests.GdsOracle` runs
  against an isolated, disposable Docker container (`topos-gds-oracle`, Community edition, own
  credentials, never a developer's real Neo4j instance) — see `docs/GDS_ORACLE_SETUP.md` for
  setup and the one real bug this harness already caught (a directed-vs-undirected projection
  mismatch in `GetBfs`). GDS per-algorithm Community/Enterprise tier verification (spec §12
  Q9) is resolved — see `docs/GDS_ALGORITHM_TIERS.md`.

---

## 10. Honest caveats

- **Code, tests, and benchmarks all exist now.** 177 tests pass (kernel, persistence, sample,
  GDS-parity); the M0 concurrency model was corrected from measured benchmark data during
  implementation, not left as inference — see `docs/M0_BENCHMARK_RESULTS_2026-07-24.md`. Don't
  trust older docs (or your own memory of them) that describe Topos as pre-code.
- **The Apple/Kuzu acquisition is weak-positive signal, not thesis validation.** Apple has
  not stated the motive; the "on-device AI" angle is one analyst's read, not Apple's
  statement. Downgraded in §1 of the investigation per reviewer Fable — this framing hasn't
  changed and doesn't need revisiting.
- **Topos is a git repository, pushed to GitHub** (`github.com/Minu476/Topos`, private).
- **The spec's residual open questions**: Q1 (paradox-compression citation) and Q7 (Handle
  generation-bits) are resolved — see `docs/PARADOX_COMPRESSION_SEARCH.md` and the `Generation`
  field already in `Handle.cs` (option (a), include-from-M0, as the spec leaned). Q9 (GDS
  algorithm tier) is resolved — see `docs/GDS_ALGORITHM_TIERS.md`. **Q2 (identity: hypergraph
  vs. incidence model) and Q10 (capability partition) are still genuinely open** — they're
  Nasser's calls, not blockers, and nothing forces resolving them.
- **RLB is in scope and the integration is live**, not just decided — see §4. The earlier "RLB
  stays untouched" framing in the oldest docs is fully superseded.
- **M8 closed 2026-07-25 (API-stability scope).** Nasser dogfooded via RLB, then a second real
  consumer (NexusVerifier) surfaced six concrete API-stability findings; resolving those plus a
  broader public-surface audit became M8's two passes — see
  `docs/NEXUS_VERIFIER_INTEGRATION_FINDINGS.md` and `docs/DECISIONS.md`'s three 2026-07-25
  entries for what was resolved and the explicit closure decision. **"M8 done" means the
  API-stability work is done, not that HIF interchange / a docs site / NuGet-publish readiness
  have happened** — those are separately deferred, each gated on a condition (forcing consumer,
  or a go-public decision) that hasn't arrived. Don't treat their absence as M8 being incomplete.
- **A separate incident worth knowing about if you ever touch the shared Neo4j Desktop
  instance** (not the GDS-oracle Docker container, a different one other local projects share):
  its credential was rotated 2026-07-24 after being typed into a chat session, and now lives in
  macOS Keychain (`security` service `neo4j-desktop`), resolved via `~/.secrets` for
  shell-based tools. Full writeup in `docs/GDS_ORACLE_SETUP.md`. Not a Topos-specific concern,
  but relevant if you're ever debugging why a Neo4j connection on this machine behaves
  differently than a doc describes.
