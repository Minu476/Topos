# Code Review — Topos (Kimi Code, 2026-09-19)

Read-only review of the full repository at commit `aae5c71` (M13). Scope: all five
`src/` packages, tests, packaging, and docs consistency. Method: direct reading of the
load-bearing source files, full `dotnet build` + `dotnet test` runs, and targeted greps.
Not a line-by-line audit of every algorithm — GDS/HNX oracle coverage is noted where it
substitutes for that.

**Verification results for this review:**
- `dotnet build Topos.sln` — succeeded, **0 warnings, 0 errors**.
- `dotnet test Topos.sln` — **352/352 passing, 0 failed** across all 8 test projects
  (kernel 240, Knowledge 19, Living 12, Persistence 18, Mcp 18, ChatMemory 20,
  GDS oracle 15, HNX oracle 10). Matches `docs/Documentation.md`'s "352 tests" claim.

---

## Verdict

This is a genuinely high-quality library — arguably the best-maintained repo in the
portfolio. The kernel is small (~4,100 lines of src total), the concurrency model is
uniform and benchmark-justified, the XML documentation is unusually honest (documented
limitations, recorded past mistakes, `[verified:src]` citations), and correctness is
backed by two independent external oracles (Neo4j GDS, HyperNetX/xgi), not just
self-consistent unit tests. Findings below are real but modest; nothing is
release-blocking.

## Findings

### Medium

1. **Snapshot reload moves `CurrentRevision`, contradicting the documented M13 contract.**
   `HypergraphKernel.RestoreVertex`'s doc (`src/Topos.Hypergraph/HypergraphKernel.cs:84-91`)
   states "A reloaded kernel starts at revision 0 with an empty log, so any consumer
   holding a pre-reload cursor gets an overflow signal and rescans." But
   `HypergraphSnapshot.Load` (`src/Topos.Hypergraph.Persistence/HypergraphSnapshot.cs:115-123`)
   replays incidences through the normal `AddIncidence`, which calls `RecordChange` and
   advances `_revision` (`HypergraphKernel.cs:150-157, 312-321`). After `Load`,
   `CurrentRevision == incidenceCount`, not 0 — and the change log (even if enabled on the
   new kernel) is empty with `_highestEvicted == 0`. A consumer holding a pre-reload cursor
   `r >= 0` gets `overflowed == false` with an empty change list: the exact silent
   staleness M13's "overflow is loud, never silent" design exists to prevent. There is no
   `RestoreIncidence` bypass, and no test covers the persistence × change-tracking
   interaction. Suggest either an incidence restore path that doesn't record changes
   (matching `RestoreVertex`), or resetting/rebasing `_revision` at the end of `Load`, plus
   a regression test.

2. **Kernel package version not bumped for M13.** `src/Topos.Hypergraph/Topos.Hypergraph.csproj`
   is still `0.2.0-m.11`, but the kernel now contains the M13 public API
   (`CurrentRevision`, `ChangesSince`, `EnableChangeLog`, `RevisionCache<T>`,
   `GraphChange`). Anyone pinning the published `0.2.0-m.11` NuGet package gets a kernel
   without the API the docs describe. Same class of issue as the earlier SemVer
   ordering bug: the publish step and the code have drifted. (`Topos.Hypergraph.Living`
   is at `0.2.0-m.12`, also unpublished, and `Topos.Hypergraph.Mcp` at `0.2.0-m.10` was
   never pushed — known and recorded in the handoff, but M13 makes the kernel's
   unpublished-drift the largest yet.)

3. **`AGENTS.md` and `docs/process/SESSION_HANDOFF.md` are stale.** `AGENTS.md` §1 still
   says "M0–M6 implemented and tested; M7 deferred; M8 done; M9 implemented; M10
   implemented" — no mention of M11 (phase 1 *and* phase 2, the latter with a live HNX
   oracle that the handoff explicitly calls "not scheduled"), M12, or M13. The handoff's
   own header says "trust the files" and was last updated 2026-08-04, but its §4.1 and §5
   decision tree now misdirect a fresh session (e.g. "M11 phase 2 … is not scheduled —
   don't start it" — it shipped in commit `7011078`). The user-facing docs
   (`Documentation.md`, `CONCEPTS.md`, `API_REFERENCE.md`, `USAGE_PATTERNS.md`) *are*
   current — Invariant 3, M12, and M13 all documented — so the staleness is confined to
   the two agent-facing files, which is the worst place for it.

### Low

4. **`changes_since` MCP tool throws on a disabled log instead of returning a structured
   error** (`src/Topos.Hypergraph.Mcp/ToposMcpServer.cs:211-221` →
   `HypergraphKernel.ChangesSince` throws `InvalidOperationException`). The tool
   description warns the agent, but a raw exception over stdio is a poor agent-facing
   signal; a `{error: "change log not enabled, call enable_change_log"}` payload would be
   kinder to tool-calling agents. Same pattern class as the deliberate `Overflowed` field
   — loud and structured beats exceptional.

5. **`semantic_recall` rebuilds its index per call** (`ToposMcpServer.cs:198-203` — a new
   `VectorIndex` per invocation, O(V) scan construction). Fine for a stateful dev-time
   server, but with M13's `RevisionCache<T>` now in the kernel, an index cached on the
   kernel's revision is a natural, cheap improvement.

6. **Minor duplication:** `ReconstructPath` exists identically in
   `IHypergraphQuery.cs:269-280` and `Knowledge/DirectedTraversal.cs:216-227`. Cross-package,
   so not trivially shareable without a public helper — acceptable, worth a note only.

7. **SWMR caveat worth restating for MCP:** `ToposMcpServer` holds a static mutable
   `_kernel` and `reset_kernel` swaps it (`ToposMcpServer.cs:22-32`). Any connected agent
   can wipe the graph; and if the host ever serves concurrent sessions, the kernel's
   single-writer assumption is silently violated. The single-session design is documented
   and deliberate — this is a "keep the doc loud as usage grows" note, not a bug.

8. **Untracked file:** `docs/M11_PHASE2_TASK_FOR_OPUS.md` is committed-never (`??` in
   git status) even though M11 phase 2 has since shipped — either commit it for
   provenance or delete it.

### Positive notes (verified, not assumed)

- **Concurrency model is uniform and clean.** One pattern everywhere: `SparseSet<T>` +
  per-pool `ReaderWriterLockSlim` (`PropertyPool.cs`, `IncidenceIndex.cs`, `ChangeLog.cs`),
  `LockRecursionPolicy.NoRecursion`, snapshot copies (`ImmutableArray`) before returning
  across lock boundaries, iterators never escape locks. The COW→RWLS pivot is recorded
  with real benchmark data, and the docs say so.
- **M13 change tracking is well-designed.** `RecordChange` writes the log before
  publishing the revision (`HypergraphKernel.cs:290-306`) — the ordering argument in the
  doc comment is correct and is the load-bearing one. `ChangeLog`'s capacity-in-distinct-
  handles semantics, loud overflow, and the recorded first-version bug
  (`ChangeLogTests.CapacityIsSpentOnDistinctHandles_NotOnWriteVolume`) show real design
  discipline. Dedicated concurrency tests exist (`ChangeLogConcurrencyTests.cs`).
- **`SparseSet.Remove`** correctly uses sparse-indirection swap-with-last (the spec §8
  disqualifying pattern avoided, with the regression test cited in the doc comment).
- **`HasCycle`'s doc** (`IHypergraphQuery.cs:282-311`) is exemplary: it warns loudly that
  the method returns `true` for almost any real hypergraph, explains why, and records the
  real consumer (NexusVerifier) that read the warning and backed off.
- **Zero TODO/FIXME/HACK markers in `src/`.**
- **Fuzz, concurrency, and stress tests exist as claimed** (`KernelFuzzTests`,
  `SparseSetFuzzTests`, `ConcurrencyTests`, `StressTests`) — AGENTS.md's claim verified.
- **Two independent oracles, both live in the suite run:** GDS (15 tests) and HNX
  (10 tests) — the "Lean-for-graph-algorithms" verification strategy is real, not
  aspirational.
- **Docs practice is strong:** the persistence class doc (`HypergraphSnapshot.cs:25-43`)
  states plainly what it is *not* (no LSM, no torn-write safety, no transparent tiering)
  — the kind of scope honesty most libraries omit.

## Suggested priorities

1. Fix finding 1 (snapshot reload vs `CurrentRevision`) — small code change, real
   correctness gap in the newest feature, needs a persistence×M13 regression test.
2. Bump the kernel package version (finding 2) before the next publish; M13's API is
   currently undocumented-at-runtime for NuGet consumers.
3. Refresh `AGENTS.md` and `SESSION_HANDOFF.md` for M11-phase-2/M12/M13 (finding 3).
4. Everything in "Low" at leisure.
