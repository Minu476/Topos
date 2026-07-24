# M0 benchmark results — 2026-07-24

**Author:** Claude (lead dev session) · **Status:** Raw measurements for the spec §6 M0 exit
gate. Per the ground rules I set for tonight's task list: **raw numbers and flagged pathologies
only — no "CSR needed/not needed" verdict here.** That's a call to make together, informed by
this data, not something to decide unilaterally mid-benchmark-run.

**Environment:** Apple M4 Pro, 12 physical cores, .NET 10.0.1, Release build.
**Job:** `ShortRun` (3 warmup + 3 measured iterations, launchCount 1) — fast, approximate numbers
for a first read, not final-precision figures. Error margins are wide on some of these (noted
inline); a longer `Job.Default` run would tighten them before this becomes a real gate/no-gate
decision. Total run time: 100 seconds across 15 benchmark cases.

Full raw log: `/tmp/benchmark_run.log` (not committed — regenerate with
`dotnet run --project benchmarks/Topos.Hypergraph.Benchmarks -c Release --no-build -- --filter '*'`).

---

## 1. SparseSet\<T\> vs Dictionary\<Handle,T\> (the vertex/property-pool access pattern)

| N | Dictionary | SparseSet | Ratio | Alloc ratio |
|---|---|---|---|---|
| 1,000 | 10.77 µs | 4.47 µs | **0.42×** (SparseSet 2.4× faster) | 0.40× |
| 100,000 | 1,464.9 µs | 656.5 µs | **0.45×** (SparseSet 2.2× faster) | 0.62× |

**Reads clean: SparseSet wins the relative gate (a) for this access pattern**, consistently ~2.2–2.4×
faster with meaningfully less allocation, at both scales tested. This is the pattern the spec's
primitive #2 (`Vertex` + reserved fields) and #4 (`PropertyKey<T>` pools) actually exercise —
the win here is real, not marginal.

## 2. Incidence index, fan-out: ConcurrentDictionary\<Handle,ImmutableArray\<Incidence\>\> vs Dictionary\<Handle,List\<Handle\>\>

N distinct sources, one member appended to each, then N lookups — the benign case (no key
contention).

| N | Naive Dictionary | Kernel (COW) | Ratio |
|---|---|---|---|
| 1,000 | 20.28 µs | 108.31 µs | **5.36× slower** |
| 20,000 | 998.65 µs | 5,843.14 µs | **5.86× slower** |

**This is the opposite of what gate (a) wants, and it's not noise — it's consistent across both
N.** The `ConcurrentDictionary` + `ImmutableArray.Create` machinery costs real overhead even in
the best case for this design (one element per key, no contention at all). The naive
single-threaded `Dictionary<Handle, List<Handle>>` wins outright here.

## 3. Incidence index, fan-in pathology: many appends into ONE shared source

This is the case flagged in `HypergraphKernel.Append`'s doc comment — a hub vertex (a
well-connected concept with many incident members) forces N appends into the same
`ImmutableArray`, each a full copy.

| N | Naive Dictionary | Kernel (COW) | Ratio |
|---|---|---|---|
| 500 | 634.5 ns | 172,708 ns | **272.68× slower** |
| 2,000 | 2,312 ns | 2,702,021 ns (2.7 ms) | **1,172× slower** |
| 8,000 | 7,957.5 ns | 55,380,480 ns (**55.4 ms**) | **6,965× slower** |

**This confirms the predicted O(N²) shape directly, not just in theory.** Naive scales ~linearly
(4× the N → ~3.6–4× the time). Kernel scales ~quadratically (4× the N → ~15.6× then ~20.5× the
time). At N=8,000 — not an extreme number for a hub concept in a real knowledge graph — building
the incidence list alone costs **55 milliseconds**. For scale: RLB's logistics step budget is
3.7ms *total*. A single hub vertex at this size would blow the entire step budget by 15×, before
any other work happens.

## 4. Hyperedge traversal: 5-hop chain walk (Q8 raw data point)

| Benchmark | Mean | Error | StdDev |
|---|---|---|---|
| `WalkFiveHopChain` | 101.2 ns | ±97.6 ns | 5.35 ns |

**Take this one with real caution** — the error margin is almost as large as the mean, which is
typical for sub-microsecond measurements dominated by JIT/branch-prediction noise at `ShortRun`
precision. The honest read is "on the order of 100ns for 5 hops, roughly 20ns/hop" — nowhere near
precise enough to nail down Q8's exact per-hop budget, but precise enough to say **traversal
latency is not the bottleneck**: even a 10× pessimistic correction (1µs for 5 hops) is trivially
inside any plausible slice of a 3.7ms step budget. The fan-in pathology above (§3) is the real
risk to the 270Hz budget, not raw traversal cost.

---

## What this means (data, not a verdict)

- **§1 (SparseSet for vertex/property storage): looks solid.** No flag.
- **§4 (traversal latency): looks solid**, with the caveat that this benchmark is too imprecise
  to answer Q8 exactly — a longer run would help if Q8 needs a hard number.
- **§2 and §3 are the real finding.** The current incidence-index design (COW
  `ImmutableArray<Incidence>` per key, chosen for lock-free reads per spec §3.4) is slower than
  the naive baseline even in the *best* case (§2), and catastrophically slower in a realistic
  worst case (§3, hub vertices). This is a genuine tension in the concurrency-model decision:
  the design bought lock-free reads at a real, measured cost, and that cost is worse than
  expected — worse than the thing it was supposed to beat.

**Not concluding "switch to CSR" or "keep the COW design" here** — that's exactly the kind of call
that should involve you, not get made silently inside a benchmark write-up. What I'd flag as the
live options, for that conversation: (a) keep COW for low-fan-in sources, fall back to a
different structure (e.g. a per-source mutable list under its own lock, sacrificing lock-free
reads for that one index) once a source's member count crosses some threshold; (b) reconsider
whether the incidence index needs to be lock-free-read at all in M0 versus a per-pool
`ReaderWriterLockSlim` like the vertex/property pools already use (same pattern as §1's winner);
(c) treat this as exactly the CSR-tier's job — frozen/compacted tiers for hub vertices, mutable
IndexMap for low-degree ones — which the spec already sketches as a *future* milestone, just
sooner than "only if benchmarks force it" implied.

---

## 5. Fix applied and re-measured (same session, after discussion)

Replaced the COW `ConcurrentDictionary<Handle, ImmutableArray<Incidence>>` incidence index with
`IncidenceIndex` — `SparseSet<List<Incidence>>` behind a `ReaderWriterLockSlim`, the same pattern
`PropertyPool<T>` already uses (§1's winner). See `src/Topos.Hypergraph/IncidenceIndex.cs`.
Full test suite (39 tests) stayed green through the change. Re-ran the two benchmarks that
flagged problems:

### Fan-in (the O(N²) pathology) — fixed

| N | Naive | Kernel (before) | Kernel (after) |
|---|---|---|---|
| 500 | 613.6 ns | 172,708 ns (272.7×) | 15,359 ns (**25.1×**) |
| 2,000 | 2,306.8 ns | 2,702,021 ns (1,172×) | 63,593 ns (**27.6×**) |
| 8,000 | 8,341.7 ns | 55,380,480 ns (6,965×) | 336,758 ns (**40.4×**) |

**The exploding growth is gone.** Ratio-to-naive went 272×→1,172×→6,965× before (each 4× step in
N costing ~15–20× more time — quadratic); now it's 25×→27.6×→40× (each 4× step in N costing
~4–5× more time — linear, with a mild constant-factor increase at the top end likely from GC
pressure, not algorithmic complexity). The dangerous unbounded-blowup case is resolved. A
25–40× constant-factor gap against naive remains — see below for why that's not a fair
comparison and isn't, on its own, a red flag.

### Fan-out — gap remains, numbers are noisy, and the comparison isn't apples-to-apples

| N | Naive | Kernel (before) | Kernel (after) |
|---|---|---|---|
| 1,000 | 20.71 µs | 108.31 µs (5.36×) | 870.39 µs (**42.05×**, ±75% error) |
| 20,000 | 970.29 µs | 5,843.14 µs (5.86×) | 5,331.72 µs (**5.5×**) |

At N=20,000 the fix is a wash (5.86× → 5.5×, within noise). At N=1,000 it looks *worse*
(5.36× → 42×), but the error bar on that measurement is ~75% of the mean — this number isn't
trustworthy at `ShortRun` precision and needs a longer run before treating it as real.

**More importantly, the naive baseline in this specific benchmark does strictly less work than
the kernel, so the ratio was never a clean apples-to-apples number to begin with:** it maintains
one direction (source→members) with no thread-safety, while `HypergraphKernel.AddIncidence`
maintains *two* directions (`_bySource` and `_byMember`) and is safe for concurrent readers
during the single writer. Roughly 2× of any gap is "doing twice the indexing work," and the rest
is real per-call overhead (lock acquire/release, `SparseSet` lookup, an `O(k)` snapshot copy per
read even for size-1 lists) that a lock-free, single-direction, unsafe `Dictionary` simply
doesn't pay. That's not nothing — it's worth a longer, cleaner benchmark before calling it
settled — but it's a materially different finding than "the design is broken," which is what §3's
original O(N²) number actually showed.

**Net assessment:** the dangerous pathology (unbounded quadratic blowup on hub vertices) is
fixed. The remaining constant-factor fan-out gap is real but modest, partly explained by doing
more work than the naive comparator — confirmed below with an actually-fair baseline.

### Fan-out, take two — a fair (two-direction, thread-safe) naive baseline

`FairFanOutBenchmarks`: naive baseline now maintains both directions under its own two
`ReaderWriterLockSlim`s — the same job the kernel does, not half of it.

| N | Fair naive | Kernel | Ratio |
|---|---|---|---|
| 1,000 | 63.21 µs | 857.49 µs | 13.57× (±63% error — noisy, don't over-read this one) |
| 20,000 | 4,658.06 µs | 5,368.70 µs | **1.15×** |

**At the more realistic scale (N=20,000), the kernel is within 15% of a fair thread-safe
baseline** — the "5.5–6× slower" headline from §2/§3's original (unfair) comparison was mostly an
artifact of comparing against a baseline doing half the work. The N=1,000 gap is still real and
unexplained (13.57×, though the ±63% error bar means this specific number isn't trustworthy) —
plausibly per-run fixed overhead (`HypergraphKernel` and its `SparseSet`s are freshly allocated
inside the benchmarked method every iteration, so small-N runs pay proportionally more setup cost
before the amortization kicks in) rather than a per-operation problem, since it mostly disappears
by N=20,000. Flagging as a minor open item, not a blocker — allocation overhead at small scale
matters far less for a long-lived in-process graph than steady-state throughput at real scale
does.

**Revised net assessment:** the O(N²) pathology is fixed and was the real problem. The
constant-factor fan-out gap, once measured fairly, is small (~15%) at realistic scale. Nothing
here argues for CSR/frozen tiers yet — the current `SparseSet` + `ReaderWriterLockSlim` design,
uniformly applied, is within noise of a fair naive baseline and meaningfully faster for the
vertex/property pattern (§1). CSR stays a "build if a real workload forces it" milestone, not a
default.

## Caveats on this data

- `ShortRun` job (3+3 iterations) — fast but wide error bars on a few numbers (noted inline).
  Worth a `Job.Default` re-run before this becomes a real go/no-go gate.
- Single-threaded only — the concurrent-load p99 case (multiple readers during the single writer,
  the actual SWMR scenario spec §3.4 describes) isn't measured here, only correctness-tested
  (`ConcurrencyTests`/`StressTests`). A concurrent-load benchmark is a reasonable next addition.
- "Failed to set up high priority (Permission denied)" appeared in the log for every benchmark —
  BenchmarkDotNet couldn't get elevated process priority in this shell. It ran anyway and
  produced consistent, repeatable-looking numbers (low StdDev relative to Mean in most cases,
  §4 being the exception), so I don't think this invalidates the results, but flagging it since
  it's the same category of issue GLM's status report claimed blocked it entirely — here it was a
  warning, not a blocker.
