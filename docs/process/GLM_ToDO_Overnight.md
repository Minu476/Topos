# GLM Overnight ToDo — 2026-07-23

**For:** GLM-5.2 (ZCode), running unsupervised overnight.
**From:** Claude (lead dev session, 2026-07-23 evening) — this file is refreshed daily; treat
only *today's* header as current. Read `AGENTS.md` first per its own §5 session-start protocol
(fsde_start_session / fsde_read_directives / fsde_get_todos) before starting anything below.

---

## 0. Where things stand right now

M0 is started, not finished. As of tonight, `src/Topos.Hypergraph/` has the four spec §3
primitives implemented and green:

- `Handle.cs`, `HandleAllocator.cs` — monotonic never-reused counter (lock-free,
  `Interlocked.Increment`) + reserved `Generation` field (Q7 lean (a): included from M0, unused
  until M4 compaction).
- `VertexRoles.cs`, `VertexStatus.cs`, `Vertex.cs` — reserved hot-path fields.
- `Incidence.cs` — raw-byte `Role`, kernel does not interpret it (layer-1 owns domain semantics).
- `PropertyKey.cs`, `PropertyRegistry.cs`, `SparseSet.cs`, `PropertyPool.cs` — EnTT-style
  sparse-set pools, one `ReaderWriterLockSlim` per pool (spec §3.4 "per-pool, not global").
- `HypergraphKernel.cs` — ties it together. SWMR concurrency model: lock-free handle allocation,
  lock-free copy-on-write incidence indexes (`ConcurrentDictionary<Handle, ImmutableArray<Incidence>>`),
  per-pool-locked vertex/property storage.
- `tests/Topos.Hypergraph.Tests/` — 26 tests, all green across 5 consecutive `dotnet test` runs
  (Handle allocation, both invariants, SparseSet swap-with-last correctness, concurrency,
  kernel API round-trips including an RLB-shaped N-ary hyperedge test).

**Not started yet:** CSR frozen tiers, the benchmark suite (M0 gate a+b), the fuzz suite proper,
M1+ (algorithms). These are intentionally *not* tonight's job — see §2.

---

## 1. Ground rules (read before touching anything)

- **Stay on a branch, not `main`.** Create `overnight/2026-07-23` off `main`, commit there in
  small logical commits, and leave it for review — do not merge or push to a remote without being
  asked. Nasser reviews each morning.
- **Do not resolve Q1, Q7, Q8, Q9, or Q10 yourself.** Every task below that touches one of these
  open questions is scoped as *evidence-gathering*, not *adjudication*. Write findings to a doc;
  do not edit `docs/SPECIFICATION.md`'s decisions or flip any 🟡 OPEN to 🔒 LOCKED.
- **Do not touch `~/Projects/Rich-Learning-Base`.** Wiring Topos as an RLB `ProjectReference` is
  cross-repo, touches a live 337-test suite, and needs an explicit go-ahead — it's on the tracked
  backlog (§2), not tonight's scope.
- **No new NuGet dependencies without flagging it.** If a task seems to need one (e.g. a
  property-based testing library), note the need in your morning report instead of adding it.
- **Every source claim keeps the `[verified:src=...]` / `[unverified:...]` discipline** — same
  standard as the rest of `docs/`.
- **At the end of the session:** update `docs/SESSION_HANDOFF.md` with what you did, and leave a
  short morning-report section at the top of this file (or a new `docs/GLM_OVERNIGHT_REPORT_2026-07-23.md`)
  summarizing results per task below.

---

## 2. Tonight's tasks, in priority order

### P0 — Benchmark harness scaffolding (M0 exit gate groundwork)

M0's exit criterion needs two measured gates (spec §6 M0): **(a) relative** — the real storage
beats a naive `Dictionary<Handle, List<Handle>>` baseline by a margin that matters; **(b)
absolute** — per-hop traversal latency under a budget derived from RLB's 270Hz figure. **Q8 (the
exact budget) is still open — do not derive or assert a pass/fail number. Just build the harness
and report raw measurements.**

1. Create `benchmarks/Topos.Hypergraph.Benchmarks/` (BenchmarkDotNet, net10.0), added to
   `Topos.sln`.
2. Implement a naive baseline: a single-threaded `Dictionary<Handle, List<Handle>>` doing the
   same workload shape as `HypergraphKernel`'s incidence index (append a member, look up all
   members of a source) and the same shape as the vertex table (`Dictionary<Handle, Vertex>` vs.
   `SparseSet<Vertex>` via `PropertyPool<Vertex>`).
3. Benchmark, at minimum:
   - `SparseSet<T>.Set/TryGet` vs. `Dictionary<Handle,T>` — add-heavy, get-heavy, and mixed
     workloads, at N ∈ {1_000, 100_000, 1_000_000}.
   - `HypergraphKernel.AddIncidence` + `IncidencesFrom` (COW `ImmutableArray` index) vs. naive
     `Dictionary<Handle, List<Handle>>` — same N range.
   - **A synthetic RLB-shaped traversal**: build a chain of N-ary hyperedges (1 Anchor, 2–3
     Conditions, 1 Target each — mirror `HypergraphKernelTests.NAryHyperedge_RoundTripsAllMembers_InOrdinalOrder`)
     and measure per-hop latency walking Anchor→Target across ~5 hops (the figure used in the
     spec's own Q8 discussion), single-threaded and under the concurrent-reader load shape from
     `ConcurrencyTests`.
4. Run it. Report raw numbers (mean, p99, allocations via `[MemoryDiagnoser]`) in
   `docs/M0_BENCHMARK_RESULTS_2026-07-23.md`. **Do not conclude "CSR is/isn't needed" — that's a
   judgment call for the morning review**, but do flag anything that looks pathological (e.g. an
   order-of-magnitude regression vs. Dictionary) since that would indicate a bug, not a design
   tradeoff.

### P1 — Q9: verify GDS per-algorithm Community/Enterprise tier

Spec §5.1 flags this as unverified from primary source. Check
`neo4j.com/docs/graph-data-science/current/` (installation + algorithms pages) directly — not
secondary sources — for whether each of **Louvain, Label Propagation, WCC, SCC, Triangle
Counting, Local Clustering Coefficient** ships in GDS *Community* Edition or requires
*Enterprise*. Write a small table with `[verified:web=url]` tags per algorithm to
`docs/GDS_ALGORITHM_TIERS.md`. If any come back Enterprise-only, note it plainly — don't soften
it — since that's exactly the gap §5.1 already anticipates for M6.

### P2 — Q1: broader "paradox-compression" search

The existing search only grepped Rich-Learning-Base. Widen it to every sibling project under
`~/Projects` (`rich-learning`, `Chimera-chess-rich-learning`, and any others present) for
`paradox` (case-insensitive) and near-neighbors like "compression finding" / "paradox
compression". Report exact hits (file:line) or a clean "still nothing" to
`docs/PARADOX_COMPRESSION_SEARCH.md`. This is bounded fact-finding — if you find something, don't
integrate it into §1 of the spec yourself; just report it, since re-anchoring the empirical
opener is Nasser's call.

### P3 — Expand fuzz + concurrency coverage

`ConcurrencyTests.cs` currently has 3 tests. M0's exit criterion calls for a "fuzz+concurrency
suite," which is more than that. Add (same project, no new packages):
- Seeded-RNG randomized `[Theory]` cases that interleave `CreateVertex` / `SetDormant` /
  `Reactivate` / `AddIncidence` / `SetProperty` from multiple threads for a fixed operation count,
  then assert kernel-wide invariants afterward (every Handle ever returned still resolves; no
  duplicate Indexes; dormant vertices never vanish).
- A `SparseSet<T>` fuzz test: randomized Set/Remove sequences (seeded), checked against a
  reference `Dictionary<Handle,T>` model after each operation (differential testing).
- Larger-N versions of the existing concurrency tests (aim for something that would realistically
  surface a race if the copy-on-write or per-pool-lock logic had a bug — current tests are
  correctness smoke tests, not stress tests).

### P4 — XML doc coverage pass

Walk every public type/member in `src/Topos.Hypergraph/` and confirm it has a doc comment in the
existing style (state the *why* — hidden invariant, spec cross-reference — not the *what*; match
the tone already in `Handle.cs`/`HypergraphKernel.cs`). Fill any gaps. Do not add comments that
just restate the member name.

---

## 3. Explicitly NOT tonight (tracked, deferred)

- CSR frozen-tier implementation — blocked on P0's benchmark numbers by design (spec's own
  discipline: don't build the fancy storage until naive/sparse-set is measured against it).
- M1 (`IHypergraphQuery` + algorithms) — M0 isn't closed yet.
- Anything touching `Rich-Learning-Base`.
- Resolving any 🟡 OPEN question in the spec.

---

*Next file: refreshed tomorrow with a new date header, informed by tonight's morning report.*
