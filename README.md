# Topos

A standalone, domain-agnostic **typed-property hypergraph library for C#**, purpose-fit for
AI / agent memory — LLM reasoning, explainability, learnable edges, tiered memory, and
provenance.

> *Topos* (Greek τόπος, "place / location") is the root of *topology*. The name invokes the
> central thesis this library serves: **knowledge stored as topological graph structure rather
> than neural-network weights.** In category theory, a *topos* is a deep theory of structured
> contexts — a fitting ambition for a foundational substrate that many domains build on.

| | |
|---|---|
| **Packages** | `Topos.Hypergraph` (kernel + algorithms) · `Topos.Hypergraph.Persistence` (save/load) · `Topos.Hypergraph.Knowledge` (directed/role-aware traversal) · `Topos.Hypergraph.Mcp` (MCP server for agent tool-calling) |
| **Target** | .NET 10 (`net10.0`), C# with `Nullable` + `ImplicitUsings` |
| **Status** | M0–M6 implemented · M7 (spectral) deferred by design · M8 API-stability scope done · M9 implemented · M10 (MCP server) implemented. 192 tests pass. |
| **License** | MIT (decided 2026-07-26 — see `docs/NUGET_PUBLISH_CHECKLIST.md`). |

---

## Get started in 5 minutes

**The combined user documentation lives in [`docs/Documentation.md`](docs/Documentation.md)** — one
file with the mental model, a runnable walkthrough, the full API reference, and usage patterns. That's
the single doc to read or hand to a reviewer.

Topos targets **.NET 10**. `Topos.Hypergraph`, `Topos.Hypergraph.Persistence`, and
`Topos.Hypergraph.Knowledge` are published on NuGet under MIT:

```bash
dotnet add package Topos.Hypergraph --prerelease
# add Topos.Hypergraph.Knowledge for directed/role-aware traversal
# add Topos.Hypergraph.Persistence for save/load
```

To track `main` instead of a released version, reference from source via a `ProjectReference`:
`[verified:src=src/Topos.Hypergraph/Topos.Hypergraph.csproj]`

```xml
<ItemGroup>
  <ProjectReference Include="path/to/Topos/src/Topos.Hypergraph/Topos.Hypergraph.csproj" />
  <!-- add Topos.Hypergraph.Knowledge for directed/role-aware traversal -->
  <!-- add Topos.Hypergraph.Persistence for save/load -->
</ItemGroup>
```

`Topos.Hypergraph.Mcp` isn't on NuGet yet. To let an MCP-aware agent (Claude Code, Cursor, etc.)
call the kernel directly without writing any C#, run it as a subprocess from source instead — see
`samples/Topos.Samples.McpAgent/` for the `.mcp.json` wiring.

Build a kernel, model one n-ary relationship (one utterance mentioning three entities = **one
hyperedge**, not three binary edges), and query it back:

```csharp
using Topos.Hypergraph;

var kernel = new HypergraphKernel();

// Define your domain's roles as a byte-backed enum (docs/ROLE_CONVENTIONS.md).
public enum TripRole : byte { Speaker = 0, Mention = 1 }

Handle alice = kernel.CreateVertex();
Handle kyoto = kernel.CreateVertex();
Handle nara  = kernel.CreateVertex();
Handle osaka = kernel.CreateVertex();

// One hyperedge — alice mentioned kyoto, nara, and osaka together, in one turn.
Handle mention = kernel.CreateVertex(VertexRoles.Edge);
kernel.AddIncidence(mention, alice, (byte)TripRole.Speaker, ordinal: 0);
kernel.AddIncidence(mention, kyoto, (byte)TripRole.Mention, ordinal: 1);
kernel.AddIncidence(mention, nara,  (byte)TripRole.Mention, ordinal: 2);
kernel.AddIncidence(mention, osaka, (byte)TripRole.Mention, ordinal: 3);

// Every algorithm on IHypergraphQuery works off the kernel directly.
bool reachable = ((IHypergraphQuery)kernel).IsReachable(alice, osaka);   // true
```

Continue end-to-end (typed properties, save/reload, directed traversal) in
**[`docs/Documentation.md`](docs/Documentation.md)** §3.

Verify your environment against the whole solution: `[verified:src=Topos.sln]`

```bash
dotnet build Topos.sln
dotnet test Topos.sln
```

---

## Documentation

**[`docs/Documentation.md`](docs/Documentation.md)** is the combined user-facing manual (concepts,
getting started, API reference, usage patterns in one file). The component docs it was assembled
from are also kept separately for readers who want a single-topic view:

| Document | Audience | Purpose |
|---|---|---|
| [`docs/Documentation.md`](docs/Documentation.md) | **All users** | **The combined manual** — read this one file, or hand it to a reviewer. |
| [`docs/CONCEPTS.md`](docs/CONCEPTS.md) | New users | The mental model (4 primitives, 2 invariants, Roles vs. VertexRoles, layers). |
| [`docs/GETTING_STARTED.md`](docs/GETTING_STARTED.md) | New users | Runnable walkthrough. |
| [`docs/API_REFERENCE.md`](docs/API_REFERENCE.md) | All users | Hand-written prose catalog of every public type. |
| [`docs/USAGE_PATTERNS.md`](docs/USAGE_PATTERNS.md) | Implementers | How to model common agent-memory shapes. |
| [`docs/ROLE_CONVENTIONS.md`](docs/ROLE_CONVENTIONS.md) | Implementers | The `byte`-backed-enum pattern for domain role bytes (settled M8). |
| [`docs/NUGET_PUBLISH_CHECKLIST.md`](docs/NUGET_PUBLISH_CHECKLIST.md) | Maintainer | MIT license decision + the executable NuGet-publish steps. |

### Why Topos exists — the design record

| Document | Purpose |
|---|---|
| [`docs/SPECIFICATION.md`](docs/SPECIFICATION.md) | The consolidated spec — approved by two review passes (`docs/reactions/`) before implementation. Opens with the verified workload case, the 4-primitive contract, the layer architecture, the M0–M9 roadmap, and §11's locked/open quick-reference. |
| [`docs/BASE_INVESTIGATION.md`](docs/BASE_INVESTIGATION.md) | Source-verified analysis of 10 libraries + the proposed storage contract. The artifact the spec was built from. |
| [`docs/AGENT_MEMORY_COMPETITORS.md`](docs/AGENT_MEMORY_COMPETITORS.md) | Source-verified survey of Zep/Graphiti, mem0, Letta, Cognee — answers "did the field reject hypergraphs, or never try them?" |
| [`docs/DECISIONS.md`](docs/DECISIONS.md) | The decision log: what reviewers locked vs. left open, what's Nasser's call, and every milestone adjudication. |
| [`docs/M0_BENCHMARK_RESULTS_2026-07-24.md`](docs/M0_BENCHMARK_RESULTS_2026-07-24.md) | The measured M0 benchmark gate and the benchmark-driven COW→per-pool-lock concurrency correction. |
| [`docs/GDS_ORACLE_SETUP.md`](docs/GDS_ORACLE_SETUP.md) | The Neo4j GDS correctness-oracle setup (Topos's standard algorithms are verified against GDS) + the local Neo4j credential-isolation writeup. |
| [`docs/GDS_ALGORITHM_TIERS.md`](docs/GDS_ALGORITHM_TIERS.md) | Resolves spec §12 Q9 — GDS Community/Enterprise tier per algorithm. |
| [`docs/PARADOX_COMPRESSION_SEARCH.md`](docs/PARADOX_COMPRESSION_SEARCH.md) | Resolves spec §12 Q1 — the "paradox-compression" citation traced to an unrelated project. |
| [`docs/NEXUS_VERIFIER_INTEGRATION_FINDINGS.md`](docs/NEXUS_VERIFIER_INTEGRATION_FINDINGS.md) | Six API-stability findings from the second real consumer — the M8 input. |
| [`docs/MCP_SERVER_SPEC.md`](docs/MCP_SERVER_SPEC.md) | The M10 proposal (approved and implemented 2026-07-27) — the forcing-function case, the tool-surface design, and the five §5 forks, four of which Nasser resolved explicitly. |
| [`docs/SESSION_HANDOFF.md`](docs/SESSION_HANDOFF.md) | Carry-on context across sessions — the first doc any agent reads on launch. |

---

## Why this exists

Every production-grade hypergraph library in the ecosystem was built for pre-AI workloads
(PDE discretization, spectral ML, VLSI partitioning, database theory, combinatorial analysis).
**No hypergraph library in any language is built as an agent-memory or LLM-reasoning
substrate.** The base investigation in `docs/` establishes this from source-level reading of
ten libraries and standards, and finds the AI-native gap confirmed by one decisive market
signal: Apple's October 2025 acqui-hire of Kuzu — the closest existing thing to an
AI-oriented embedded graph database — for on-device AI / privacy-focused graph processing.

---

## Status

**M0–M6 implemented; M7 (spectral) deferred by design; M8's API-stability scope is done; M9
implemented; M10 (MCP server) implemented.** The specification (`docs/SPECIFICATION.md`) was
reviewed and approved before implementation started; `docs/DECISIONS.md` tracks what's locked vs.
still open. M8's other spec items (HIF interchange, a docs site, NuGet publishing) are deliberately
deferred pending a forcing consumer or a decision to go public — not in progress.

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
- **M9** — `Topos.Hypergraph.Knowledge` package: layer-1 role-aware directed traversal
  (`DirectedBfs`/`DirectedShortestPath`/`RoleFilteredMembers`, `AddIncidence<TRole>`), generalizing
  a pattern three independent consumers (ChatMemory, RLB, NexusVerifier) had each hand-rolled. Exit
  criterion met: RLB's `ToposGraphProjection` now calls into it instead of its own copy.
- **M10** — `Topos.Hypergraph.Mcp` package: a Model Context Protocol server exposing the kernel and
  `Topos.Hypergraph.Knowledge` as 18 agent-callable tools (Microsoft's official `ModelContextProtocol`
  C# SDK, stdio transport, stateful single-session). See `docs/MCP_SERVER_SPEC.md` for the design
  and `docs/DECISIONS.md`'s "M10 APPROVED AND IMPLEMENTED" entry for the build record.

192 tests pass across the kernel, persistence, sample, Knowledge, Mcp, and GDS-parity suites. Topos is
also a live `ProjectReference` in **Rich-Learning-Base**'s V2 codebase, not just a design target —
see `Learning/ToposGraphProjection.cs` there — with RLB's own suite (346 tests, including live
Neo4j round-trips) passing against it.

---

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

## Provenance and integrity

The investigation docs and the original specification draft (`docs/BASE_INVESTIGATION.md`,
`docs/AGENT_MEMORY_COMPETITORS.md`, `docs/SPECIFICATION.md`) were authored in ZCode by GLM-5.2,
under a source-verification discipline — every claim tagged `[verified:src=…]`,
`[verified:spec=…]`, `[verified:paper=…]`, `[verified:web=…]`, or `[unverified:inferred]`, the
same standard NexusVerifier's `SOLVED_PROBLEMS.md` enforces for formal proofs (verified vs.
axiom-scaffolded vs. `sorry`). No unsourced assertions.

The specification was then reviewed and approved by two review passes (`docs/reactions/`,
`docs/DECISIONS.md`) before implementation started. The codebase and later working docs have
multiple AI contributors across sessions rather than a single author — M0 is explicitly credited
"lead dev: Claude/Sonnet" in its commit message, and several `docs/*.md` files (e.g.
`M0_BENCHMARK_RESULTS_2026-07-24.md`, `PARADOX_COMPRESSION_SEARCH.md`,
`GDS_ALGORITHM_TIERS.md`) carry their own `**Author:**` line — check individual commit messages
and doc headers for specific attribution rather than assuming ZCode/GLM-5.2 authored everything
in the repo.
