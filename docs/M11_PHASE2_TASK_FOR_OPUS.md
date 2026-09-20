# Task for Opus 5: M11 Phase 2 — s-connectivity family

**Status: task brief, no code written yet.** Written 2026-09-16 following a review of this
project's health and open work from the TradingSystem side (Topos is now a dependency of both
Rich-Learning-Base and TradingSystem's Nasser System research). Not a cold assignment — Opus 5
already has direct history here (`docs/DECISIONS.md`, 2026-08-03, "NUGET VERSION SCHEME
CORRECTED," decider Nasser, executed by Opus 5).

## Why this, why now

Project health check before recommending this: 251/251 tests passing across all 7 test
assemblies (`Topos.Hypergraph.Tests`, `.Knowledge.Tests`, `.Living.Tests`, `.Mcp.Tests`,
`.Persistence.Tests`, `Topos.Tests.GdsOracle`, `Topos.Samples.ChatMemory.Tests` — verified
2026-09-16 via `dotnet test Topos.sln`). This isn't a project that needs rescuing — it's a
well-run one with real, identified work left. `docs/ALGORITHM_GAP_LIST.md`'s six-item M11
must-have shortlist has **three items done** (Centrality, PageRank, Directed SCC — M11 Phase 1,
`DECISIONS.md` 2026-07-30/08-03, GDS-verified live) and **three items fully unstarted**:
s-connected-components, s-line-graph, s-diameter/eccentricity. This is that remaining work.

These three are exactly the kind of thing worth a strong model's attention specifically: they're
**novel-by-nature** — no GDS equivalent exists (GDS operates on binary graphs; these are
genuinely n-ary hypergraph concepts), so there's no single ground-truth oracle to check against.
`ALGORITHM_GAP_LIST.md` §3-4 already worked out the honest verification ladder for exactly this
situation (T2 differential cross-implementation, T3 metamorphic property testing, T5 reduction
anchors) — this is thin-verification-surface work where a subtle bug is easy to miss and hard to
catch later, and it feeds two real downstream consumers (RLB's learning kernel,
TradingSystem's proposed Living-Graph signal engine), so correctness here has real reach.

## Scope

The implementation spec already exists — **execute it, don't re-derive it**:

- `docs/ALGORITHM_SPEC.md` §5.1 — s-connected-components (Kernel, MUST)
- `docs/ALGORITHM_SPEC.md` §5.2 — s-line-graph (Kernel, MUST)
- `docs/ALGORITHM_SPEC.md` §5.3 — s-diameter / s-eccentricity (Kernel, MUST)

Verification oracle: `docs/ALGORITHM_SPEC.md` §3 (HyperNetX/HNX — a new
`tests/Topos.Tests.HnxOracle` project, mirroring `tests/Topos.Tests.GdsOracle`'s established
pattern). Use the verification ladder from `ALGORITHM_GAP_LIST.md` §2-4 as specified per item:
s-connected-components and s-diameter get T2 (HNX cross-check); s-line-graph gets T2 against
*both* HNX and xgi (two independent lineages, per the gap list's own reasoning for why that pair
was chosen). Add T3 metamorphic properties and T5 reduction anchors (s-walk at s=1 must equal
the kernel's existing standard BFS/connectivity) per §4's own worked examples — don't invent a
different verification approach than the one this project already designed for this exact
situation.

## One open fork blocking the start — needs Nasser's actual sign-off, not assumed

`ALGORITHM_SPEC.md` §6.1 is explicitly flagged "🟡 your calls gate scope" and has never been
formally decided (no corresponding `DECISIONS.md` entry, unlike §6.3's PageRank-adjacency fork
and §6.4's milestone-naming fork, both resolved in the Phase 1 entries): **Docker sidecar vs.
Python subprocess for the HNX oracle.** The spec's own recommendation is (a) Docker sidecar,
for parity with the GDS-oracle discipline. Confirm this with Nasser before building the oracle
harness — same "record and confirm before treating as final" discipline this project has used
throughout, not something to silently assume even though the recommendation is clear.

## Process discipline (matches this project's own established conventions — don't deviate)

- A `DECISIONS.md` entry for this milestone, following §6.4's already-settled convention: new
  "M11 Phase 2" entry, not folded into a closed milestone.
- Full test suite stays green throughout; report the before/after count (currently 251).
- Layer discipline intact per `CONCEPTS.md`/`ALGORITHM_SPEC.md` §4 (🔒 LOCKED architecture) — no
  storage-contract changes, no concurrency-model changes; these are read-only algorithms over
  `IHypergraphQuery`, same constraint §8 already states for the whole spec.
- No NuGet publish without a separate, explicit go-ahead — same discipline as every prior publish
  decision in this project's history, and the exact thing Opus's own prior session here had to
  correct once (the `-mN`→`-m.N` versioning bug) — get the version bump right this time from the
  start, not after a live consequence.

## Deliverable

Matches `ALGORITHM_SPEC.md` §7's exit criterion, items 1 and 4 specifically: all three items
implemented and HNX-verified (parity tests pass when the oracle is reachable, skip-graceful
when not — matching the GDS-oracle pattern's own graceful-degradation discipline), and the test
count grown from 251 with the new algorithm tests, full suite green. Items 2/3/5 of that exit
criterion (RLB and NexusVerifier actually adopting the new surface as real consumers) are
**not** required to close this task — RLB is mid-repair on its own correctness issues right now
(a separate, parallel workstream), so forcing cross-repo adoption before that lands would be
premature. Note it as the natural follow-on once RLB is stable, not a blocker here.
