# Topos

A standalone, domain-agnostic **typed-property hypergraph library for C#**, purpose-fit for
AI / agent memory — LLM reasoning, explainability, learnable edges, tiered memory, and
provenance.

> *Topos* (Greek τόπος, "place / location") is the root of *topology*. The name invokes the
> central thesis this library serves: **knowledge stored as topological graph structure rather
> than neural-network weights.** In category theory, a *topos* is a deep theory of structured
> contexts — a fitting ambition for a foundational substrate that many domains build on.

## Why this exists

Every production-grade hypergraph library in the ecosystem was built for pre-AI workloads
(PDE discretization, spectral ML, VLSI partitioning, database theory, combinatorial analysis).
**No hypergraph library in any language is built as an agent-memory or LLM-reasoning
substrate.** The base investigation in `docs/` establishes this from source-level reading of
ten libraries and standards, and finds the AI-native gap confirmed by one decisive market
signal: Apple's October 2025 acqui-hire of Kuzu — the closest existing thing to an
AI-oriented embedded graph database — for on-device AI / privacy-focused graph processing.

## Status

**M0–M6 implemented; M7 (spectral, deferred by design) and M8 (OSS polish) remain.** The
specification (`docs/SPECIFICATION.md`) was reviewed and approved (GPT + Claude passes,
`docs/reactions/`) before implementation started; `docs/DECISIONS.md` tracks what's locked vs.
still open.

- **M0** — storage kernel: `Handle`/`Vertex`/`Incidence`/`PropertyKey<T>`, the 2 invariants, SWMR
  concurrency (`ReaderWriterLockSlim`-per-pool). Benchmark-gated per spec §6, including a
  benchmark-driven redesign after measured data — see `docs/M0_BENCHMARK_RESULTS_2026-07-24.md`.
- **M1** — `IHypergraphQuery` + traversal algorithms (BFS/DFS/shortest-path/cycle/SCC/transitive
  closure), verified against a real Neo4j GDS oracle (`tests/Topos.Tests.GdsOracle`; setup in
  `docs/GDS_ORACLE_SETUP.md`).
- **M2–M4** — reification (asserted/quoted/hypothesized mode), composable views + set algebra,
  tiered persistence (`Topos.Hypergraph.Persistence`, package split at this boundary).
- **M5** — embeddings (`PropertyKey<float[]>` + ANN), learnable edge weights, provenance — plus
  the falsifiability gate: a non-RLB second consumer (`samples/Topos.Samples.ChatMemory`).
- **M6** — s-walk traversal, label propagation, triangle count, modularity.

173 tests pass across the kernel, persistence, sample, and GDS-parity suites. Topos is also a live
`ProjectReference` in **Rich-Learning-Base**'s V2 codebase, not just a design target — see
`Learning/ToposGraphProjection.cs` there — with RLB's own suite (346 tests, including live Neo4j
round-trips) passing against it.

## Relationship to other projects

**Build strategy (decided 2026-07-23): Topos is built as Rich-Learning-Base's kernel first**,
with standalone-library ambition as a falsifiable M5 milestone (a non-RLB second consumer).
See `docs/DECISIONS.md` §6.

- **Rich-Learning-Base** (`~/Projects/Rich-Learning-Base`) — **first consumer, live.** Topos
  is a `ProjectReference` in RLB's V2 csproj (added during M0–M4) — see
  `src/RichLearning.V2/Learning/ToposGraphProjection.cs`, which does hyperedge-aware pathfinding
  over a Topos `HypergraphKernel` built from RLB's landmarks/transitions/hyperedges. RLB's
  346-test suite, including live Neo4j round-trips, passes against it. **Dependency direction
  preserved:** Topos references nothing upstream (no RLB types leak into Topos); RLB references
  Topos.
- **FSDE** (`~/Projects/FSDE`) — still decoupled. May adopt Topos later.

**Verification strategy:** Neo4j GDS (Graph Data Science) is the correctness oracle for
Topos's standard algorithms — the Lean-for-NexusVerifier pattern applied to graph
algorithms. See `docs/DECISIONS.md` §6 and `AGENTS.md` §9.

## Local development — Neo4j credentials

The GDS-parity oracle (`tests/Topos.Tests.GdsOracle`) runs against an isolated, disposable Docker
container (`topos-gds-oracle`, non-default ports, its own throwaway credentials) — never against
any developer's real Neo4j instance. See `docs/GDS_ORACLE_SETUP.md` for how to stand it up.

Separately, if you also run a general-purpose Neo4j instance on this machine (e.g. Neo4j Desktop,
used by other local projects like Rich-Learning-Base and FSDE) — as of 2026-07-24 that instance is
bound to `127.0.0.1` only, auth-enabled, and its password lives in **macOS Keychain**
(`security` service `neo4j-desktop`), not in any repo or plaintext file. Consumers resolve it
per-context: interactive shells via `~/.secrets` (which reads Keychain at shell-start), and
non-shell processes like FSDE's launchd daemon via a small wrapper script that reads the same
Keychain entry. Full incident writeup, including why a hardcoded `.env` copy is the wrong pattern
here, in `docs/GDS_ORACLE_SETUP.md`.

## Documents

| Document | Purpose |
|---|---|
| [`docs/SPECIFICATION.md`](docs/SPECIFICATION.md) | **The consolidated spec — under GPT+Claude review.** Opens with the verified RLB empirical case, incorporates the 4-primitive contract, GPT's 5-layer architecture, the resolved open questions, and the M0–M8 roadmap with Neo4j GDS verification + falsifiable M5. §12 lists the open questions for reviewers. |
| [`docs/BASE_INVESTIGATION.md`](docs/BASE_INVESTIGATION.md) | Source-verified analysis of 10 libraries + the proposed storage contract + roadmap skeleton. The artifact Fable and GPT enhance into the final spec. |
| [`docs/AGENT_MEMORY_COMPETITORS.md`](docs/AGENT_MEMORY_COMPETITORS.md) | Source-verified survey of the four systems competing for the agent-memory niche (Zep/Graphiti, mem0, Letta, Cognee) — answers "did the field reject hypergraphs, or never consider them?" Includes the n-ary-DB capability matrix (TypeDB/TigerGraph) and hypergraph-research-prototype preemption. |
| [`docs/GDS_ORACLE_SETUP.md`](docs/GDS_ORACLE_SETUP.md) | How the GDS-parity oracle container is set up, and the local Neo4j credential-isolation writeup above. |

## Provenance and integrity

Authored in ZCode by GLM-5.2. Every claim in the investigation is tagged
`[verified:src=…]`, `[verified:spec=…]`, `[verified:paper=…]`, `[verified:web=…]`, or
`[unverified:inferred]` — the same discipline NexusVerifier's `SOLVED_PROBLEMS.md` enforces
for formal proofs (verified vs. axiom-scaffolded vs. `sorry`). No unsourced assertions.
