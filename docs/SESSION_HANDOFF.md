# Session Handoff — Topos (M0–M6 implemented; M8 API-stability scope done; M9 implemented; M10 implemented)

**Last updated:** 2026-07-27
**Purpose:** Comprehensive briefing so the next session (in Topos, possibly with a different
agent) can resume without re-deriving context. **This is the first document to read.**

If this handoff contradicts what you observe in the files, **trust the files** — and update
this handoff.

---

## 1. What Topos is (read this first if context is lost)

**Topos** (`/Users/nassertowfigh/Projects/Topos`, pushed to `github.com/Minu476/Topos`,
private) is a standalone, domain-agnostic **typed-property hypergraph library for C#**,
purpose-fit for AI / agent memory. The name (*topos* = Greek for "place/location", root of
*topology*) invokes the central thesis: knowledge stored as topological graph structure rather
than neural-network weights.

**Status: M0–M6 implemented and tested. M7 (spectral) deferred by design. M8's API-stability
scope is done. M9 is implemented (2026-07-26). M10 is implemented (2026-07-27).** — Nasser
dogfooded via Rich-Learning-Base first, then a second real consumer (NexusVerifier) produced six
concrete API-stability findings; resolving those plus a broader public-surface audit became M8's
two passes, both done 2026-07-25 (`docs/DECISIONS.md`'s three 2026-07-25 entries, the last one an
explicit closure decision). **"M8 done" means the API-stability work — HIF interchange, a docs
site, and NuGet-publish readiness are separately deferred, each gated on a condition that hasn't
arrived (a forcing consumer, or a go-public decision), not part of what closed.** M9
(`Topos.Hypergraph.Knowledge`, layer-1 role-aware directed traversal) was scoped 2026-07-25 and
**built and closed out 2026-07-26**: `DirectedBfs`/`DirectedShortestPath`/`RoleFilteredMembers`/
`AddIncidence<TRole>` shipped with 11 new tests, and its exit criterion is met — RLB's
`ToposGraphProjection` now calls into the new package instead of its own hand-rolled copy, with
RLB's 346-test suite passing unchanged. M10 (`Topos.Hypergraph.Mcp`, an MCP server exposing the
kernel as agent-callable tools) was proposed overnight 2026-07-26 (GLM-5.2, `docs/MCP_SERVER_SPEC.md`)
and **approved and built the next day, 2026-07-27**: 18 tools over the official
`ModelContextProtocol` C# SDK, stdio transport, stateful single-session, 13 new tests, dogfooded
via raw JSON-RPC over the real transport (agent-in-the-loop dogfood still pending a Claude Code
restart). See §4 for what all of this means concretely and §5 for what to do about it.

Two things worth internalizing before anything else:
- **The spec was approved and implemented — this is not still the investigation phase.**
  Earlier drafts of this handoff (and some AI-authored docs still in `docs/`) describe a
  pre-code "base-investigation phase." That phase ended weeks ago. If you're reading an old
  cached summary of this project, or another agent's memory of it, distrust anything that says
  "no code yet."
- **Topos has a real second consumer already, not just a design target.** Rich-Learning-Base's
  V2 codebase has a live `ProjectReference` to Topos (`src/RichLearning.V2/Learning/
  ToposGraphProjection.cs`), and RLB's own 346-test suite — including live Neo4j round-trips —
  passes against it. This isn't aspirational; it's verified, checked again as recently as this
  session.

---

## 2. What's on disk right now

```
~/Projects/Topos/
├── README.md                         # public-facing intro — kept in sync with real status
├── AGENTS.md                         # workspace instructions — read every session
├── Topos.sln
├── src/
│   ├── Topos.Hypergraph/             # the kernel (M0–M6, see §3)
│   ├── Topos.Hypergraph.Persistence/ # tiered LRU+snapshot persistence (M4 package split)
│   ├── Topos.Hypergraph.Knowledge/   # M9: layer-1 role-aware directed traversal (new 2026-07-26)
│   └── Topos.Hypergraph.Mcp/         # M10: MCP server, 18 tools (new 2026-07-27)
├── tests/
│   ├── Topos.Hypergraph.Tests/
│   ├── Topos.Hypergraph.Persistence.Tests/
│   ├── Topos.Hypergraph.Knowledge.Tests/  # M9 package tests (new 2026-07-26)
│   ├── Topos.Hypergraph.Mcp.Tests/        # M10 package tests (new 2026-07-27)
│   └── Topos.Tests.GdsOracle/        # Neo4j GDS-parity oracle — docs/GDS_ORACLE_SETUP.md
├── samples/
│   ├── Topos.Samples.ChatMemory(.Tests)/  # M5's non-RLB second consumer
│   └── Topos.Samples.McpAgent/            # M10: .mcp.json wiring example (new 2026-07-27)
├── benchmarks/
│   └── Topos.Hypergraph.Benchmarks/  # BenchmarkDotNet suite
└── docs/
    ├── Documentation.md             # USER-FACING: the COMBINED manual (concepts+getting-started+API
    │                                  # reference+usage patterns in one file, with unified TOC + in-file
    │                                  # anchors). New 2026-07-26. The single doc to read or hand to a
    │                                  # reviewer/GPT (solves the "GPT can't navigate the GitHub file tree"
    │                                  # problem — one file, no navigation needed). Updated for NuGet (MIT).
    ├── CONCEPTS.md, GETTING_STARTED.md, API_REFERENCE.md, USAGE_PATTERNS.md  # USER-FACING docs (new
    │                                  # 2026-07-26, GLM-5.2): the mental model, a runnable walkthrough,
    │                                  # a hand-written public-API catalog, and a usage-patterns guide.
    │                                  # README.md is the front door. Documentation.md combines all four.
    │                                  # The rest of docs/ predates them and is investigation/spec/process.
    ├── NUGET_PUBLISH_CHECKLIST.md     # decision recorded 2026-07-26 (license = MIT); executable
    │                                  # checklist for the gated NuGet-publish item. Resolves the
    │                                  # "waits on Nasser deciding a license" item in "M8 CLOSED"
    │                                  # (docs/DECISIONS.md) — execution is .csproj edits + dotnet pack/push,
    │                                  # outside GLM-5.2's doc-only lane; a code session executes it.
    ├── MCP_SERVER_SPEC.md             # M10 (proposed overnight 2026-07-26, APPROVED AND IMPLEMENTED
    │                                  # 2026-07-27): MCP server exposing the kernel + Knowledge package as
    │                                  # 18 agent-callable tools, built on Microsoft's official
    │                                  # ModelContextProtocol C# SDK (v1.4.1, Apache-2.0 — the doc's original
    │                                  # "MIT" claim was corrected). Nasser approved + resolved all four §5
    │                                  # forks that gate v1 scope; see docs/DECISIONS.md's "M10 APPROVED AND
    │                                  # IMPLEMENTED" entry for the full build record.
    ├── BASE_INVESTIGATION.md, AGENT_MEMORY_COMPETITORS.md, SPECIFICATION.md, DECISIONS.md
    ├── M0_BENCHMARK_RESULTS_2026-07-24.md   # measured benchmark gate, COW→RWLS correction
    ├── PARADOX_COMPRESSION_SEARCH.md        # resolves spec §12 Q1
    ├── GDS_ALGORITHM_TIERS.md               # resolves spec §12 Q9
    ├── GDS_ORACLE_SETUP.md                  # GDS Docker setup + the Neo4j credential incident
    ├── NEXUS_VERIFIER_INTEGRATION_FINDINGS.md  # M8 input: 6 findings, resolved 2026-07-25
    ├── ROLE_CONVENTIONS.md                  # M8: documented role-byte-enum pattern (finding #3)
    ├── SESSION_HANDOFF.md                   # this file
    └── reactions/                            # verbatim GPT/Claude review rounds
```

192 tests pass across the kernel, persistence, sample, Knowledge, Mcp, and GDS-parity suites (up
from 179 before 2026-07-27's M10 build — 13 new tests in `Topos.Hypergraph.Mcp.Tests`). Build:
`dotnet build Topos.sln`. Test: `dotnet test Topos.sln`.

---

## 3. What's been built (compressed history — read the docs for detail, don't re-derive it)

**Investigation and spec phase** (2026-07-23): `docs/BASE_INVESTIGATION.md` (10-library
survey), `docs/AGENT_MEMORY_COMPETITORS.md` (Zep/mem0/Letta/Cognee survey — closed the "did the
field reject hypergraphs or never try them" question, answer: never seriously tried),
`docs/SPECIFICATION.md` (the consolidated spec), `docs/DECISIONS.md` (the decision log). Two
strategic forks were resolved by Nasser: build Topos as RLB's kernel first (not decoupled), and
use Neo4j GDS as the correctness oracle for standard algorithms. GPT and Claude both reviewed
and approved the spec before implementation started (`docs/reactions/`).

**Implementation** (2026-07-23 through 2026-07-24, six commits):
- **M0** — storage kernel: `Handle` (with `Generation` field, resolving spec Q7 as
  "include from M0"), `Vertex`, `Incidence`, `PropertyKey<T>`, the 2 invariants, SWMR
  concurrency. The concurrency model was **corrected by measured data during implementation**:
  the spec's original copy-on-write design measured 5–6× slower than a naive baseline and
  O(N²) on hub vertices (55ms for one 8,000-member incidence list); replaced with
  `SparseSet<T>` + `ReaderWriterLockSlim`-per-pool, which measured 2.2–2.4× *faster* than naive
  and eliminated the pathology. Full data in `docs/M0_BENCHMARK_RESULTS_2026-07-24.md` — this
  is the benchmark-driven-development the spec's M0 gate was designed to force, and it worked.
- **M1** — `IHypergraphQuery` + ~40 default-method traversal algorithms, verified against a
  real Neo4j GDS oracle (`tests/Topos.Tests.GdsOracle`, isolated Docker container — see
  `docs/GDS_ORACLE_SETUP.md`). This harness caught one real bug: a directed-vs-undirected
  projection mismatch in `GetBfs`.
- **M2–M4** — reification (asserted/quoted/hypothesized mode), composable views + set algebra,
  tiered persistence (`Topos.Hypergraph.Persistence`, package split at this boundary as
  planned).
- **M5** — embeddings (`PropertyKey<float[]>` + ANN), learnable edge weights, provenance, and
  the **falsifiability gate**: `samples/Topos.Samples.ChatMemory`, a non-RLB second consumer,
  proving the kernel serves a domain it wasn't designed around.
- **M6** — s-walk traversal, label propagation, triangle count, modularity.

**RLB integration** — not a separate future step, already done as part of M0–M4: Topos is a
`ProjectReference` in RLB's V2 csproj; `ToposGraphProjection.cs` does hyperedge-aware
pathfinding over a Topos kernel built from RLB's landmarks/transitions/hyperedges; RLB's
346-test suite (up from 337 at spec-writing time) passes against it, including live Neo4j
round-trips.

**This session (2026-07-24, later in the day)** — three threads, none of them roadmap work:

1. **Full honest codebase + spec review**, requested directly by Nasser. Verified claims
   against actual code/tests/git history rather than trusting the docs' own framing — found the
   engineering itself (benchmark-driven concurrency redesign, real GDS-oracle verification, real
   RLB consumer) to be genuinely solid, but flagged the surrounding review-process documentation
   (`docs/reactions/`, `DECISIONS.md`'s "GPT reviewed and approved 9.5/10" framing) as one
   operator running multiple AI personas against each other and recording it like independent
   peer review — real engineering signal, but the "two independent reviewers" framing shouldn't
   be read as external validation.
2. **A Neo4j credential/security incident, found incidentally and fixed end-to-end.** A
   *separate* Neo4j Desktop instance on this machine (not the GDS-oracle Docker container —
   shared by RLB, FSDE, and TradingSystem) was found bound to all network interfaces with auth
   disabled. Fixed: rebound to `127.0.0.1`-only, auth re-enabled, password rotated after the old
   one was typed into this chat session (treated as compromised on principle). The new
   credential lives in macOS Keychain (`security` service `neo4j-desktop`) — `~/.secrets`
   resolves it for shell-based tools (RLB, this repo's own dev use), and FSDE's launchd daemon
   (which doesn't inherit shell env) got a small wrapper script doing the same Keychain lookup.
   Downstream consumers were checked, not just the instance itself: RLB's live Neo4j tests still
   pass; FSDE's `.env` had an unrelated stale/quoted password that was also fixed; FSDE's 21
   `Api`-suite test failures turned out to be a pre-existing, unrelated mock gap
   (`FsdeApiFactory` never stubs `IDriver.AsyncSession`), not a credential issue. Full writeup:
   `docs/GDS_ORACLE_SETUP.md`. **This is not a Topos bug or Topos scope** — recorded here
   because it happened during a Topos session and touches infrastructure other Topos work might
   rely on (the GDS-oracle container itself was never affected).
3. **Nasser chose to use Topos hands-on before continuing the roadmap.** Explicitly declined to
   start M8 (API stability review was offered) in favor of dogfooding via RLB first. Built
   `tools/ToposHyperedgeDemo` in the RLB repo — a small runnable console demo (not a unit test)
   showing the old hyperedge-blind path-finder failing where the Topos-backed one succeeds,
   because RLB's real accumulated Neo4j data (`dapsa-learning`, `fuguerl`) turned out to have
   *no hyperedges in it yet* (worth knowing — the thing Topos's whole thesis is about isn't
   actually populated in real RLB data yet). Separately, explored whether NexusVerifier (a Lean
   4 theorem-proving project with its own hypergraph engine and a *previously paused* RLB
   integration experiment) is a good candidate for a future "many agents as solver over a
   hypergraph" direction — verdict: promising and not speculative (the domain is structurally an
   AND-OR hypergraph, and a prior RLB experiment there is well-documented with a clear NO-GO
   reason and a named re-entry path), but **not started** — Nasser said "I will start doing that
   with you later." Also reviewed (separately, a different project) TradingSystem for whether to
   adopt RLB+Topos there — recommendation was to sequence it *after* an already-planned August
   cleanup, not bundle them, given TradingSystem trades real funds and a prior RLB integration
   attempt there was archived (reason unknown, worth asking Nasser before repeating the pattern).
   README updated to match all of the above (Status, RLB relationship, Documents table,
   Provenance section all were stale and are now current).

**This session (2026-07-25)** — resumed M8, scoped to
`docs/NEXUS_VERIFIER_INTEGRATION_FINDINGS.md`'s six findings (see §4.1 for the summary and
`docs/DECISIONS.md`'s 2026-07-25 entry for full detail). Nasser made the two real API-freeze
calls via explicit questions before any code was touched (fix the doc for cell properties, wire
`Handle.Invalid` into `TryGetVertex` failures) and opted into all three ergonomic items
(role-convention doc, `HasCycle` amplification, layer-1 traversal confirmation). Files touched:
`Incidence.cs`, `Handle.cs`, `HypergraphKernel.cs`, `FilteredView.cs`, `UnionView.cs`,
`IHypergraphQuery.cs`, new `docs/ROLE_CONVENTIONS.md`, new regression tests in
`HypergraphKernelTests.cs`/`ViewsTests.cs`, plus this handoff, `AGENTS.md`, and `DECISIONS.md`.

**Second pass, same session:** asked to decide what's next for M8, so scoped and ran a broader
read-only audit (forked agent) of the rest of the public surface, then resolved it — see
`docs/DECISIONS.md`'s second 2026-07-25 entry. Two real API-tightening decisions (again via
explicit questions): `PropertyKey<T>`'s constructor is now `internal` (was public, could
construct colliding-Id-different-T keys), and `SparseSet<T>` is now `internal` (was the one
outlier public storage-plumbing type) with `InternalsVisibleTo` added for
`Topos.Hypergraph.Tests`/`Topos.Hypergraph.Benchmarks` — the repo's first use of
`InternalsVisibleTo`. Also fixed a real bug found along the way, not in the original audit:
`SWalk.Reachable`'s argument-validation was deferred to enumeration (a bare iterator method
footgun) instead of throwing eagerly like `SWalk.Distance` — now eager, with a regression test.
Plus doc-only fills on `IHypergraphQuery`, `HypergraphKernel`, `HypergraphViews`,
`LearnableEdge`, `VectorIndex`, `Modularity`. Also decided (not asked, since neither had a
forcing consumer): HIF interchange and a docs site are deferred, same discipline as M7's
deferral; package version strings corrected from stale `0.1.0-m0`/`0.1.0-m4` to `0.1.0-m8`.
177 tests pass (up from 173 at the start of this session).

**Asked directly "what's next for M8," Nasser chose to close M8's API-stability scope as-is**
(over making the license call now, or holding off entirely) — see `docs/DECISIONS.md`'s third
2026-07-25 entry. Two API-audit passes (a findings-driven one and a broader sweep) is the
declared-complete scope; nothing further is pending on the API-stability front.

**M9 (2026-07-25 scoped, 2026-07-26 implemented):** `Topos.Hypergraph.Knowledge` — layer-1
role-aware directed traversal (`DirectedBfs`/`DirectedShortestPath`/`RoleFilteredMembers` +
`AddIncidence<TRole>`), generalizing a pattern three independent consumers (ChatMemory,
NexusVerifier, RLB's `ToposGraphProjection`) had each hand-rolled. Exit criterion met: RLB's
`ToposGraphProjection` refactored to consume it; RLB's 346-test suite passes unchanged. See
`docs/DECISIONS.md`'s "M9 SCOPED" and "M9 IMPLEMENTED" entries.

**The 2026-07-26/27 documentation push (GLM-5.2, multiple sessions):** built Topos's first
user-facing documentation set — `docs/CONCEPTS.md`, `GETTING_STARTED.md`, `API_REFERENCE.md`,
`USAGE_PATTERNS.md`, then consolidated them into a single `docs/Documentation.md` (1,800+ lines,
all `[verified:src=...]`-cited). Plus `docs/build_pdf.py` (one-command Markdown→PDF generator),
`docs/NUGET_PUBLISH_CHECKLIST.md` (MIT license decision recorded, executable publish steps), and
the regenerated `docs/Documentation.pdf` (44 pages, vector, clickable TOC).

**M10 (2026-07-26 proposed overnight by GLM-5.2, 2026-07-27 approved + built by Sonnet 5):**
`Topos.Hypergraph.Mcp` — an MCP server exposing the kernel + Knowledge as 18 agent-callable tools,
built on Microsoft's official `ModelContextProtocol` C# SDK (v1.4.1, Apache-2.0). All four §5
design forks resolved explicitly by Nasser (stateful single-session, stdio transport, opaque-string
Handles, tagged-union properties). 13 new tests pass; full Topos suite 192/192. **Verified at the
protocol level** — real `initialize`/`tools/list`/`tools/call` JSON-RPC frames confirmed over the
actual stdio transport. The one remaining gate is literal agent-in-the-loop dogfood (a Claude
Code restart to pick up the new `topos` entry in `.mcp.json`). See `docs/DECISIONS.md`'s "M10
APPROVED AND IMPLEMENTED" entry.

**Latest session (GLM-5.2, 2026-07-27, after Opus 5 finished M10):**
- Added **§6 "MCP server — agent tool-calling"** to `docs/Documentation.md` (~165 lines, 6
  subsections: what-it-is / wire-it-into-an-agent / the 18 tools / worked example / what's-not-
  exposed / state-lifetime). All tools mapped to their wrapped primitives with `[verified:src=...]`
  citations. Regenerated the PDF (now 44 pages, 54 internal links).
- **Deleted `docs/MCP_SERVER_REVIEW.md`** — a premature review I wrote *before* Opus 5 finished.
  It misrepresented the current state (said dogfooding wasn't done — it was; said the SDK was MIT —
  it's Apache-2.0). Removed rather than leave a misleading artifact.
- **Fixed a stale license tag** in `docs/MCP_SERVER_SPEC.md`'s Sources section (Opus 5 had already
  corrected the body; I caught the straggler).
- **Cross-project work (separate repo):** wrote NexusVerifier's first user-facing documentation set
  at `~/Projects/NexusVerifier/docs/` — `CONCEPTS.md`, `GETTING_STARTED.md`, `CLI_REFERENCE.md`,
  unified `architecture.md` (replacing a 61-line stub), `CONFIGURATION.md`. Surfaced and fixed three
  README-vs-code discrepancies in NexusVerifier (the `NEXUS_PARTS_NATIVE_DECIDE` values, the
  `NEXUS_NEO4J_DATABASE` default, the Gemini env-var name). All uncommitted there, awaiting
  Nasser's review.

---

## 4. The three things that matter most for the next session

### 4.1 Roadmap state: M6 done, M7 skip, M8's API-stability scope is done (closed 2026-07-25), M9 implemented (2026-07-26), M10 implemented (2026-07-27)

**2026-07-25 update:** the six `docs/NEXUS_VERIFIER_INTEGRATION_FINDINGS.md` findings were
resolved this session — see `docs/DECISIONS.md`'s first 2026-07-25 entry for the full list of
what was decided and what changed. Summary: fixed the one real doc-vs-code bug (`Incidence.cs`'s
cell-property claim), locked the `Handle.Invalid`/`Generation` conventions (wired `Invalid` into
`TryGetVertex` failures across the kernel and both views; `Generation` stays reserved with
corrected doc wording), documented a role-byte-enum convention (`docs/ROLE_CONVENTIONS.md`,
no kernel change), amplified the `HasCycle` doc warning, and confirmed role-aware traversal
stays out of the kernel (candidate for a future M9+ `Knowledge` package).

**M8 is now closed for its API-stability scope**, per Nasser's explicit call (third 2026-07-25
entry in `docs/DECISIONS.md`) after a broader public-surface audit (second entry) found nothing
further to fix. **Don't reopen API-stability work on Topos without a new forcing signal** (a
fourth consumer, a bug report, etc.) — re-auditing a surface that was just deliberately declared
stable is exactly the busywork this project's discipline avoids. What's still open, separately:
HIF interchange and a docs site (deferred, re-entry conditions logged) and NuGet-publish
readiness (license + package metadata — Nasser's call, not made yet).

**M9 was formally scoped 2026-07-25** (fourth entry in `docs/DECISIONS.md`, also
`docs/SPECIFICATION.md` §6's new M9 row) — asked directly "what's next for M9," Nasser chose to
scope it now rather than wait or start the NexusVerifier thread first. M9 is a new
`Topos.Hypergraph.Knowledge` package: layer-1 role-aware directed traversal
(`DirectedBfs`/`DirectedShortestPath`/`RoleFilteredMembers` over `IHypergraphQuery`). The forcing
evidence was unusually strong — investigating this surfaced a **third** independent reinvention of
the same pattern beyond the two (ChatMemory, NexusVerifier) already on record: RLB's own
`RichLearning.V2.Learning.ToposGraphProjection.cs` already contained a full generic
`DirectedBfs`/`DirectedShortestPath` implementation written entirely against Topos's public API
(zero RLB types) — meaning M9's core was largely an extraction-and-generalization of ~40
already-working lines, not new design.

**M9 was built and its exit criterion met 2026-07-26** (see `docs/DECISIONS.md`'s "M9
IMPLEMENTED" entry) — Nasser asked to start M9 and to do the RLB refactor in the same session
rather than splitting them. `src/Topos.Hypergraph.Knowledge/` now exists: `DirectedTraversal.cs`
(`DirectedBfs`/`DirectedShortestPath`/`RoleFilteredMembers`, as extension methods on
`IHypergraphQuery`) and `RoleExtensions.cs` (`AddIncidence<TRole>` plus `TRole`-typed traversal
overloads, turning `docs/ROLE_CONVENTIONS.md`'s byte-backed-enum convention into real code, with
an `ArgumentException` guard if `TRole` isn't actually byte-backed). 11 new tests in
`tests/Topos.Hypergraph.Knowledge.Tests/`. **RLB's `ToposGraphProjection.cs` is refactored**: its
three private methods (`DirectedBfs`, `DirectedShortestPath`, `Reconstruct`) are deleted, replaced
by calls into `Topos.Hypergraph.Knowledge.DirectedTraversal`; `RichLearning.V2.csproj` gained the
new `ProjectReference`. **RLB's 346-test suite passes unchanged** — confirmed by running
`dotnet test tests/RichLearning.V2.Tests`, same result as before the refactor. Nothing further is
pending on M9; if a future session wants to extend it (e.g. surfacing role-aware cycle detection,
or the `Knowledge` package NexusVerifier's chainer could also adopt), that needs a new forcing
signal, not speculative extension.

M7 (spectral machinery) stays deferred — three voices (investigation + both reviewers) agreed,
and nothing since has forced it. Don't start it without a concrete forcing requirement.

**M10 was proposed overnight 2026-07-26 (GLM-5.2, `docs/MCP_SERVER_SPEC.md`) and approved +
implemented 2026-07-27** (see `docs/DECISIONS.md`'s "M10 APPROVED AND IMPLEMENTED" entry) —
Nasser was shown two things found by checking the proposal rather than trusting it (the SDK's real
license is Apache-2.0, not the doc's original "MIT" claim; `~/Projects/FSDE` already runs its own
hand-rolled, non-SDK MCP server — a live counter-precedent), gave an explicit go-ahead on a
different bar than M9's (M9 required three independent prior reinventions as forcing evidence;
M10 was approved on the thesis-alignment case alone, a conscious departure the spec's own §9 named
as a risk), then answered all four §5 forks that gate v1 scope explicitly (stateful single-session,
stdio-only, opaque-string handles, tagged-union property values). `src/Topos.Hypergraph.Mcp/` now
exists: `ToposMcpServer.cs` (18 `[McpServerTool]` methods — vertex/incidence CRUD, typed property
get/set/remove, kernel-level query, M9's directed traversal, `semantic_recall`), `TypeMapping.cs`
(Handle wire parsing, the property tagged union), `Transport/StdioHost.cs` (the stdio host). 13 new
tests in `tests/Topos.Hypergraph.Mcp.Tests/`. `samples/Topos.Samples.McpAgent/` has the `.mcp.json`
wiring example, and this repo's own `.mcp.json` now has a `topos` entry (additive — `fsde`'s entry
is untouched). **Dogfooded via raw JSON-RPC over the real stdio transport** — all 18 tools verified
to register with correct schemas, and a full create-vertex → add-incidence → traversal round trip
verified to behave exactly per the kernel's documented semantics. **Not yet dogfooded through a
live agent session** — that's the one item left open, and it needs Nasser to restart Claude Code
so this session (or the next one) picks up the new `topos` `.mcp.json` entry.

### 4.2 The Neo4j credential pattern, if you ever touch that instance

Not Topos-specific, but if a future session needs to interact with the shared local Neo4j
Desktop instance (distinct from the GDS-oracle Docker container): the password lives in macOS
Keychain, not in any file. `security find-generic-password -a "$(whoami)" -s "neo4j-desktop"
-w` retrieves it; `~/.secrets` already exports it as `NEO4J_PASSWORD` for interactive shells.
Never hardcode it in an appsettings.json/`.env` again — that's exactly the pattern that broke
three separate consumers (FSDE's daemon, and multiple TradingSystem tools) when it was rotated
tonight. Full incident record: `docs/GDS_ORACLE_SETUP.md`.

### 4.3 NexusVerifier — a real, well-grounded future direction, not started

If Nasser brings this up: the relevant context is in `~/Projects/NexusVerifier/docs/
hypergraph-engine.md` (their own in-Lean AND-OR hypergraph engine, with a stated Phase 2 goal —
move from string-keyed to canonically-keyed, which is exactly what Topos's `PropertyKey<T>`
model already does) and `~/Projects/NexusVerifier/docs/RLB_V2_FUTURE_WORK.md` (a paused RLB
integration experiment with a precise, data-grounded NO-GO reason — the FC100 benchmark corpus
has almost no branching or shared subgoals — and a named re-entry path: re-ingest at
full-Mathlib scale, where real lemma reuse should create real junctions). Don't propose starting
this without re-reading that future-work doc's §4 pre-flight checklist first — it's the
methodology that should gate any new attempt, per the project's own stated lesson.

---

## 5. What to do when the next session starts (decision tree)

**Read AGENTS.md and this handoff first. Then:**

- **If Nasser wants to continue the roadmap** → M8's API-stability scope is closed (2026-07-25);
  what's left there is gated (HIF/docs-site need a forcing consumer or go-public decision;
  NuGet-publish needs a license choice). **M9 is implemented and its exit criterion is met**
  (2026-07-26) — `Topos.Hypergraph.Knowledge` exists, RLB's `ToposGraphProjection` consumes it, and
  RLB's suite passes unchanged. **M10 is implemented** (2026-07-27) — `Topos.Hypergraph.Mcp`
  exists, dogfooded via raw JSON-RPC; the one open item is a live-agent dogfood pass (see §4.1),
  which just needs Claude Code restarted to pick up the new `.mcp.json` entry, not more building.
  There is no pending M9 or M10 work otherwise; a next roadmap slice would be a new milestone
  (M11+) with its own forcing signal. M7 stays skipped absent a forcing requirement. Don't restart
  API-stability work without a new forcing signal.
- **If Nasser wants to continue dogfooding via RLB** → `tools/ToposHyperedgeDemo` in the RLB
  repo is the existing hands-on entry point; RLB's real Neo4j data has no hyperedges yet, which
  is itself worth discussing (is that expected, or a sign RLB's HyperEdge model isn't actually
  being exercised in practice?).
- **If Nasser wants to start the NexusVerifier thread** → see §4.3. Start with their own
  pre-flight checklist against a Mathlib-scale extract before building anything.
- **If Nasser wants to dogfood the new MCP server through a live agent** → restart Claude Code (or
  whichever MCP-aware agent) so it picks up the `topos` entry in `.mcp.json`, then create a couple
  of vertices, add an incidence, and call `bfs`/`role_filtered_members` — the literal M10 exit
  criterion, only partially demonstrated so far (raw JSON-RPC, not a live agent).
- **If Nasser wants to commit and/or go public** → ⚠️ **nothing from this session or Opus 5's M10
  work is committed yet.** The working tree has substantial uncommitted state: M9 (build +
  RLB-integration not yet committed either, last commit is `a3a2cd8`), M10 (src + tests + sample +
  .mcp.json + 5 doc-file changes), the entire 2026-07-26/27 documentation push (4 new docs +
  Documentation.md + build_pdf.py + NUGET_PUBLISH_CHECKLIST + the regenerated PDF), and the
  NexusVerifier cross-project docs (separate repo). **Two things to do before committing Topos:**
  (1) scrub the `/Users/nassertowfigh/Projects/Topos/...` absolute path in `.mcp.json`'s new
  `topos` entry (the same class of path-leak flagged in the earlier security pre-flight); (2)
  decide what to do with `.zcode/` — it's not in `.gitignore` and shouldn't be committed.
  **NexusVerifier's working tree also has unrelated untracked items** (`phase4_results/`,
  `ToDoList/`, `fsde-model-catalog-fix/`) — don't sweep those into a doc commit; stage
  explicitly.
- **If Nasser wants to continue the NexusVerifier documentation thread** → he's currently reading
  the 5 docs I wrote at `~/Projects/NexusVerifier/docs/` (CONCEPTS, GETTING_STARTED,
  CLI_REFERENCE, architecture, CONFIGURATION). Feedback may surface revisions. Three known follow-
  ups I deliberately left for him: the duplicate root-level `architecture.md` (now superseded by
  `docs/architecture.md`), and `SOLVED_PROBLEMS.md` + `SPEC.md` are likely stale (June, predate
  the FC100 correction) and may still cite the 28%/7-of-10 figures the README now calls wrong.
- **If nothing's specified** → ask. Don't assume M8/M10 follow-on work; don't assume more
  dogfooding; don't assume NexusVerifier. All are live threads, none is default.
- **Do not re-litigate the storage contract, the RLB-kernel-first decision, or the GDS-oracle
  verification strategy** — all three are implemented and working, not open questions.

---

## 6. Honest caveats for the next session

1. **Don't trust older docs' self-description over the actual repo state.** Several docs in
   `docs/` (and possibly this handoff's own older cached versions) describe a pre-code phase.
   `git log`, `dotnet test`, and the actual `src/`/`tests/` tree are authoritative, not prose.
2. **The "two independent reviewers approved this" framing in `docs/reactions/` and
   `DECISIONS.md` is real content but not independent validation** — it's one operator running
   multiple AI personas. Treat locked decisions as "the author considered alternatives and
   picked one carefully," not as external consensus. This doesn't mean the decisions are wrong —
   the engineering underneath them (measured benchmarks, real GDS verification, a real second
   consumer) is genuinely solid — just don't cite the review theater as independent proof.
3. **Don't re-derive the four-primitive contract, the RLB build strategy, or the GDS
   verification strategy from scratch.** All settled, all implemented, all working.
4. **The competitor survey and paradox-compression citation are both closed** —
   `docs/AGENT_MEMORY_COMPETITORS.md` and `docs/PARADOX_COMPRESSION_SEARCH.md` respectively.
   Don't re-open either without new evidence.
5. **Maintain the `[verified:src]` discipline** in any new investigation-style doc — it's a real
   asset of this project's documentation culture, independent of the review-process caveat in
   §6.2 above.
6. **RLB's real accumulated data has no hyperedges in it yet** (§3, §4.2 above) — worth keeping
   in mind before assuming the RLB integration is being exercised under realistic conditions
   just because the tests pass. Synthetic test coverage and unit-test scenarios are solid; real
   production data exercising the hyperedge path specifically is not yet demonstrated.
7. **GLM-5.2's role in this repo is now a standing, enforced boundary, not an informal norm** —
   `docs/GLM_DOCUMENTATION_GUIDELINES.md` (new 2026-07-26): documentation only, never `src/` code,
   including doc comments (a stricter line than a past session's XML-doc-coverage pass, which did
   touch `.cs` files — that practice is now retired). If a future session sees GLM edit anything
   under `src/`/`tests/`/`samples/`/`benchmarks/`/`tools/`, that's a violation of this guideline,
   not a return to old practice.

---

## 7. Open practical items for Nasser

- **Git: done.** Topos is a git repo, pushed to `github.com/Minu476/Topos` (private). Nothing
  outstanding here.
- **The Medium piece on Kuzu's storage engine** (mentioned in older handoffs, HTTP 403 on
  fetch) — still unread, still potentially useful before any M4-adjacent persistence work
  resumes, but M4 is done and nothing is currently blocked on it. Low priority.
- **The three live-but-unstarted threads from §4**: roadmap resumption, continued RLB
  dogfooding, and the NexusVerifier direction. All are Nasser's call on sequencing; none is
  default next action.
- **Two design ideas considered and deferred this session (2026-07-27, GLM-5.2 + GPT + Opus 5
  cross-check):** a "fuzzy/hypothesis cloud" layer and a "dream layer" (offline generative
  consolidation / concept invention). Both **deferred on M9's falsifiability bar** — surveyed
  RLB and ChatMemory; neither hand-rolls the pattern (zero-for-two). The interesting finding for
  the dream layer: RLB **already has** offline consolidation — the "Sleep Cycle"
  (`Rich-Learning.V2/Engine/DapsaKernel.cs:755-767`, `SynthesizeMetaGraphAsync`) does centroid-
  coarsening and macro-edge aggregation. The cost split here inverts GPT's "this is more novel
  than the fuzzy layer" opener for *half* the pitch: the consolidation half is **0% missing** —
  RLB already ships it — which makes it *less* novel than the fuzzy layer (which has a ~20% gap:
  time-based decay and materialized per-vertex clouds have no consumer pulling for them). Only
  the **invention** half of the dream idea — generating new concepts that are *not* centroids, and
  predicting edges between currently-unconnected vertices — has zero precedent in Topos, RLB, or
  any of the four competitors surveyed in `docs/AGENT_MEMORY_COMPETITORS.md`. And that novel core
  is a *learning system* (generator + discriminator over candidates), not a storage capability,
  and would pull in ML dependencies the kernel deliberately doesn't carry — its right home is a
  **consumer** (RLB's Sleep Cycle growing a generative mode, or a future NexusVerifier inference
  mode), not the kernel. **Two things
  recorded so this isn't re-litigated:** (1) `docs/CONCEPTS.md` now explicitly states that
  provenance (`AssertionMode`) and confidence (`EdgeStatistics.Confidence`) are orthogonal axes,
  to preempt the "add a HypothesisVertex/ImaginedVertex subclass" move; (2) the forcing trigger to
  watch for is a second consumer actually hand-rolling a per-vertex candidate cloud, a
  `Promote()`/`Decay()` mechanic, or a timestamp read for aging — none of which exist today.
  Don't spec or build either without that forcing signal.

---

## 8. Provenance and integrity note

This handoff (2026-07-24 rewrite) was authored by Claude (Sonnet) during a session that also
did the honest codebase review, the Neo4j credential incident response, the README updates, and
the RLB `ToposHyperedgeDemo` demo referenced above. Earlier phases of this project (the
investigation docs, the original specification draft) were authored in ZCode by GLM-5.2 under a
source-verification discipline (`[verified:src=…]` tags) — see those docs' own headers for
specific attribution rather than assuming single authorship across the whole repo. If anything
in this handoff contradicts the actual files, trust the files and fix this handoff.
