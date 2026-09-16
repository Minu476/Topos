# M13 change-tracking benchmark results — 2026-09-16

**Author:** Opus 5 · **Status:** Raw measurements for `LIVING_GRAPH_SPEC.md` §3.4 and §7's exit
item 2 ("writer-side cost measured and published — a number, not a claim").

Following the M0 write-up's ground rule deliberately: **numbers and flagged concerns here, no
unilateral design verdict.** The one call this data clearly supports — keeping the log opt-in — was
already made in §6.1 before the measurement; everything else it raises is noted as a question, not
resolved.

**Environment:** Apple M4 Pro, .NET 10, Release build. `ShortRunJob` (3 warmup + 3 measured
iterations) — fast approximate numbers, wide error bars on some rows, same precision caveat the M0
run carried. Regenerate with:

```bash
dotnet run --project benchmarks/Topos.Hypergraph.Benchmarks -c Release -- --filter '*ChangeLogBenchmarks*'
```

---

## 1. What change tracking costs the writer

`_LogDisabled` is what every existing consumer gets today: the revision counter always on, the log
off. It is the honest baseline for "what did M13 cost me," since a pre-M13 kernel no longer exists
to measure against.

| Benchmark | N | Mean | vs. log disabled | Allocated |
|---|---|---|---|---|
| `CreateVertex_LogDisabled` | 10,000 | 108.32 µs | — | 770.84 KB |
| `CreateVertex_LogEnabled` | 10,000 | 321.34 µs | **2.97×** | 981.12 KB |
| `SetProperty_LogDisabled` | 10,000 | 87.38 µs | — | 7.40 KB |
| `SetProperty_LogEnabled` | 10,000 | 214.56 µs | **2.46×** | 217.68 KB |
| `CreateVertex_LogDisabled` | 100,000 | 1,134.97 µs | — | 6,147.71 KB |
| `CreateVertex_LogEnabled` | 100,000 | 3,731.83 µs | **3.29×** | 6,359.42 KB |
| `SetProperty_LogDisabled` | 100,000 | 840.63 µs | — | 7.40 KB |
| `SetProperty_LogEnabled` | 100,000 | 2,011.05 µs | **2.39×** | 217.68 KB |

**The absolute number, which is the one that matters:** the log costs
**≈12 ns per property write and ≈21–26 ns per vertex creation.** Multiplying out, a workload doing
100,000 writes pays about 1.2–2.6 ms in total for change tracking.

**The ratio, which is the one that looks alarming:** 2.4–3.3×. Both readings are true and they are
not in tension — a kernel write is *cheap*, so a fixed ~12 ns addition is a large fraction of it
while remaining negligible against any real per-step budget (RLB's is 3.7 ms/step).

**This is why §6.1's opt-in call was right, and the data supports it after the fact:** a consumer
that never reads the feed would have paid a 2.4–3.3× write tax for nothing. It also says the
always-on revision counter is the right thing to leave always on — it is inside the `_LogDisabled`
rows already, and those are in line with pre-M13 expectations.

## 2. Does coalescing actually hold under a hot handle?

The claim the first implementation got wrong (and a unit test caught): repeated writes to one handle
must update its slot in place, not consume ring capacity.

| Benchmark | N | Mean |
|---|---|---|
| `SetProperty_LogEnabled` (64 handles) | 10,000 | 214.56 µs |
| `SetProperty_LogEnabled_OneHotHandle` | 10,000 | 185.03 µs |
| `SetProperty_LogEnabled` (64 handles) | 100,000 | 2,011.05 µs |
| `SetProperty_LogEnabled_OneHotHandle` | 100,000 | 2,045.51 µs |

**Clean result, no flag.** A single hot handle costs the same as writes spread across 64 — slightly
cheaper at N=10,000, within noise at N=100,000. Write volume does not scale the log's cost, and (per
`ChangeLogTests.CapacityIsSpentOnDistinctHandles_NotOnWriteVolume`) it evicts nothing. The capacity
really is a budget of distinct handles.

## 3. ⚠️ The read side allocates, and that is worth a decision

| Benchmark | Mean | Allocated |
|---|---|---|
| `ChangesSince_FullRing` (100 calls, 2,048 entries each) | ~2,035 µs | 10,022 KB |

That is **≈20 µs and ≈100 KB per call**. The allocation is inherent to the current shape: every
`ChangesSince` builds a fresh `List<GraphChange>` and sorts it (the sort is the cost of updating
slots in place — see `ChangeLog`'s class doc).

**Flagged, not fixed.** For the intended usage this is a non-issue, because the intended usage is:

```csharp
long now = kernel.CurrentRevision;      // O(1), allocation-free
if (now == cursor) return;              // the common case — nothing changed
foreach (var change in kernel.ChangesSince(cursor, out bool overflowed)) { ... }
```

A consumer that guards on the counter calls `ChangesSince` only when something genuinely moved, and
the 100 KB is amortized across real work. A consumer that calls it in a tight poll loop without the
guard will produce steady Gen0 pressure, and nothing in the API stops it.

Three options if that turns out to matter, none taken here: an allocation-free overload that fills a
caller-supplied buffer; keeping the returned list sorted incrementally on the write side instead;
or documenting the counter-guard pattern hard enough that the tight-loop case does not arise.
**`USAGE_PATTERNS.md` Pattern 12 takes the third for now** — it is free, and the other two are
premature without a consumer that has actually hit this.

---

## What this means (data, not a verdict)

- **§1 — the log's writer cost is real in ratio, negligible in absolute terms.** Opt-in was the
  right default; no flag.
- **§2 — coalescing works as specified.** No flag.
- **§3 — the read path allocates ~100 KB per call.** The only flagged item. Benign under the
  documented usage, unbounded under undisciplined polling, and worth revisiting if a real consumer
  polls hard. Not resolved here.
- **No argument for a lock-free ring.** The `ReaderWriterLockSlim` design is comfortably inside any
  plausible budget, and M0's record is what a clever alternative would have to beat.
