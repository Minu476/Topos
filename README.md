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

**Base-investigation phase.** No code yet, by design. The investigation document is the
input to the final specification, which is being written by
[Fable / GPT review](https://github.com) (see `docs/BASE_INVESTIGATION.md`, §7 — open
questions for the spec writers).

The intended roadmap (M0 storage kernel → M8 OSS polish) is sketched in the investigation
document, §6. Realistic calibration for an *industrial-level* C# library: months, not weeks.

## Relationship to other projects

**Build strategy (decided 2026-07-23): Topos is built as Rich-Learning-Base's kernel first**,
with standalone-library ambition as a falsifiable M5 milestone (a non-RLB second consumer).
See `docs/DECISIONS.md` §6.

- **Rich-Learning-Base** (`~/Projects/Rich-Learning-Base`) — **first consumer.** Topos
  becomes a `ProjectReference` in RLB's V2 csproj during M0–M4. RLB's 337-test suite is the
  first real validation. **Dependency direction preserved:** Topos references nothing
  upstream (no RLB types leak into Topos); RLB references Topos.
- **FSDE** (`~/Projects/FSDE`) — still decoupled for now. May adopt Topos later.

**Verification strategy:** Neo4j GDS (Graph Data Science) is the correctness oracle for
Topos's standard algorithms — the Lean-for-NexusVerifier pattern applied to graph
algorithms. See `docs/DECISIONS.md` §6 and `AGENTS.md` §9.

## Documents

| Document | Purpose |
|---|---|
| [`docs/BASE_INVESTIGATION.md`](docs/BASE_INVESTIGATION.md) | Source-verified analysis of 10 libraries + the proposed storage contract + roadmap skeleton. The artifact Fable and GPT enhance into the final spec. |

## Provenance and integrity

Authored in ZCode by GLM-5.2. Every claim in the investigation is tagged
`[verified:src=…]`, `[verified:spec=…]`, `[verified:paper=…]`, `[verified:web=…]`, or
`[unverified:inferred]` — the same discipline NexusVerifier's `SOLVED_PROBLEMS.md` enforces
for formal proofs (verified vs. axiom-scaffolded vs. `sorry`). No unsourced assertions.
