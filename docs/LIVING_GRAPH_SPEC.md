# Topos — Living-Graph Spec (M13 proposal)

**Status:** proposal, nothing built. Written 2026-09-16 by Opus 5, at Nasser's request, after an
M12 readiness review turned up a gap M12's own scoping pass concluded too early.

Structured deliberately like `docs/ALGORITHM_SPEC.md` (scope → layer discipline → per-item spec →
open forks → exit criterion), because that document's shape worked: M11 shipped in two clean phases
off it, and its §6 forks are what kept the judgment calls with Nasser instead of buried in code.

---

## 1. Why this spec exists — the gap this closes

M12 asked what it would take to make a Topos hypergraph "live/adaptive rather than a disposable
snapshot," worked through a wishlist, and concluded that **one** item wasn't already covered by
composable primitives: `PropertyKey<T>` is present-only, so an entity had no timeline.
`Topos.Hypergraph.Living` closed that. `[verified:docs=docs/DECISIONS.md 2026-09-06]`

That conclusion was right about the half it examined and stopped one step early. "Living" has two
halves:

- **Recording change** — the graph carries its own history. M12 shipped this. It is sound.
- **Noticing change** — something downstream can tell that the graph moved, and what moved.
  **Nothing in Topos does this.**

The evidence, from a direct read of the kernel rather than from the docs:

- **No change notification of any kind.** A grep of `src/Topos.Hypergraph/` for `event`,
  `EventHandler`, `INotify*`, `Changed`, `Revision`, `Epoch` or `Version` returns **zero** hits
  outside doc comments. `[verified:src=src/Topos.Hypergraph/*.cs]`
- **No way to ask "what changed since I last looked."** Only to rescan and diff yourself.
- **One real affordance exists, and it is narrow.** Handles are monotonic and never reused, so
  `Subgraph(kernel, h => h.Index > lastSeen)` is exactly "vertices added since" with no persistence
  layer — `HypergraphViews`' own doc points this out.
  `[verified:src=src/Topos.Hypergraph/HypergraphViews.cs:29-33]` But it catches **additions only**.
  A property overwrite, a dormancy transition, a reinforced `LearnableEdge` — none of them move any
  cursor. They are invisible to it.
- **Views are genuinely live, but "live" means recomputed.** `Mask`'s predicate re-evaluates on
  every call — correct design — while `VertexHandles()` returns a fresh snapshot list each time. So
  keeping a view current is O(V) per poll, and nothing tells a caller when a poll is worth making.
  `[verified:src=src/Topos.Hypergraph/HypergraphViews.cs:39-43, IHypergraphQuery.cs:69]`

**The one-line statement of the gap:** recording history is how you reconstruct the past; it does
not help anyone notice that the present moved. Today every Topos consumer that wants to stay in
sync must poll the whole graph and diff it by hand. That is the cost this spec removes.

---

## 2. The bucketing (judged for Topos specifically)

### MUST-have — the gap is not closed without these

| Item | Why it's a MUST |
|---|---|
| **1. Revision counter** (kernel) | Turns "has anything changed?" from an O(V) rescan into an O(1) integer compare. Cheapest possible win, and every other item builds on it. |
| **2. Change log + `ChangesSince(revision)`** (kernel) | Turns "what changed?" from a hand-written diff into a query. This is the actual deliverable; item 1 alone only tells you to go look. |
| **3. Decide and document incidence mutability** | Not code, necessarily — a decision. See §6.5. A change feed has to be able to *express* every kind of change the graph admits, so "can a relation lose a member?" has to be settled before the feed's vocabulary is fixed. |

### GOOD-to-have — real value, deliberately not phase 1

| Item | Why it waits |
|---|---|
| **4. Push notification** (event / `IChangeToken`-style callback) | Polling a revision integer covers most consumers at near-zero cost. Push adds callback-reentrancy and lifetime questions to a model that currently has none. Add it when a consumer demonstrably can't poll. |
| **5. MCP `changes_since` tool** | The obvious 20th tool, and the natural way an agent notices its own world model moved. Depends on items 1–2 existing first. |
| **6. Revision-cached views** | A view that recomputes only when the revision moves — turns the O(V)-per-poll cost above into O(V)-per-actual-change. Pure consumer-side convenience over items 1–2. |

### NO-need (against the thesis) — explicitly out of scope

| Item | Why not |
|---|---|
| **A full event-sourced replay log** | `Topos.Hypergraph.Living` already records semantic history, and `Topos.Hypergraph.Persistence` already durably snapshots state. A kernel-level log that could rebuild the graph from zero would be a *second* source of truth for history, competing with both. The change feed answers "what moved recently," not "what is the history of this entity" — that question already has an owner. |
| **Cross-process / distributed change feed** | The feed is an in-process signal for in-process consumers. Shipping changes between processes is the persistence layer's problem, and a much larger one. |
| **Undo / rollback / time-travel reads** | Requires MVCC-style versioned storage — a storage-contract change, which §8 rules out. Snapshot-and-reload already covers the coarse case. |

---

## 3. The verification problem — there is no oracle for this

M11 had HyperNetX. **M13 has no external oracle at all**: a change feed is not a published algorithm
with a reference implementation to compare against, and no competing library implements *Topos's*
notion of a change. So this falls squarely into `ALGORITHM_GAP_LIST.md` §4's "novel-by-nature"
regime, and gets verified the same way — by properties and reductions rather than by parity.

The good news: **the strongest available witness is already in the repo**, and it is unusually good.

### 3.1 T5 reduction anchor — the brute-force diff oracle

The change feed makes a falsifiable claim that can be checked against an independent computation:

```
Capture full kernel state S₀ at revision r.        // vertices, incidences, every property
Perform an arbitrary sequence of writes.
Capture full kernel state S₁.

Assert: ChangesSince(r), reduced to a set of affected handles,
        == Diff(S₀, S₁), computed by brute force.
```

`Diff` is written once, in the test project, in the dumbest possible way — walk both snapshots,
compare. It is slow and obviously correct, which is exactly what an oracle should be. This is a
genuine T1-grade check that happens to be in-house rather than external, and it is stronger than
anything M11 phase 2 had for its novel regime.

The machinery to capture state already exists: `HypergraphKernel.EnumerateProperty` (which
`Topos.Hypergraph.Persistence` already uses for its columns) plus `VertexHandles`/
`GetHyperedgeVertices`. `[verified:src=src/Topos.Hypergraph.Persistence/PersistedProperty.cs:23]`

### 3.2 T3 metamorphic properties

Hold for any correct implementation, no reference needed:

```
1. Monotonic:      revision never decreases.
2. Exactly-once:   each write method bumps the revision by exactly 1.
3. No-op honesty:  a write that changes nothing observable still bumps (or documented not to — §6.2).
4. Empty at head:  ChangesSince(CurrentRevision) is empty.
5. Total at zero:  ChangesSince(0) covers every handle a full scan finds.
6. Nesting:        r ≥ r'  ⟹  ChangesSince(r) ⊆ ChangesSince(r').
7. No duplicates:  a handle appears at most once per ChangesSince result (given coalescing, §6.2).
8. Overflow is loud: a reader that fell behind a bounded log is TOLD, never silently shorted (§6.3).
```

Property 8 is the one that matters most and is easiest to get wrong. A change feed that silently
drops changes is worse than no change feed, because consumers will trust it.

### 3.3 Concurrency verification — the part that needs real care

This is the first Topos feature whose correctness depends on the threading model rather than merely
respecting it. It needs its own test class, not assertions bolted onto the functional tests:

- One writer thread performing a known sequence; N reader threads polling `ChangesSince`.
- Assert: no reader observes a torn record, no reader misses a change without an overflow signal,
  and the union of what readers saw equals what the writer did.
- Run it under stress (thousands of writes) rather than a token few, because this class of bug does
  not reproduce at small N.

### 3.4 The benchmark gate — non-negotiable here, for a documented reason

**Every write in Topos pays for this feature.** That is exactly the situation the M0 benchmark gate
exists for, and this project has already been burned once by getting it wrong in the same area:
the original copy-on-write incidence index chased lock-free reads and measured **5–6× slower than a
naive `Dictionary` in the benign case, and O(N²) — thousands of times slower — for a hub vertex**.
The fix was the boring option: `SparseSet<T>` behind a `ReaderWriterLockSlim`, which is now the one
concurrency pattern for the whole kernel. `[verified:docs=docs/M0_BENCHMARK_RESULTS_2026-07-24.md §2-3, §5]`
`[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:9-30]`

**The lesson applies directly.** The obvious instinct for a change log is a clever lock-free ring
buffer. The precedent says: build the boring one first (same RWLS pattern as every other pool),
benchmark the writer-side cost with the log enabled and disabled, and only then decide whether
anything cleverer is earned. Benchmarks land in `benchmarks/Topos.Hypergraph.Benchmarks` alongside
the existing five classes.

---

## 4. What stays where — the layer discipline (🔒 LOCKED architecture)

- **The revision counter must be kernel-level.** Only `HypergraphKernel` sees writes; nothing above
  it can count them. There are exactly seven write methods to hook — `CreateVertex`,
  `RestoreVertex`, `SetDormant`, `Reactivate`, `AddIncidence`, `SetProperty<T>`, `RemoveProperty<T>`
  — a small, closed, auditable set. `[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:57-164]`
- **The change log's storage is kernel-level too**, for the same reason.
- **The consumption API's home is a genuine fork** — kernel or `Topos.Hypergraph.Living`. See §6.4.
- **No storage-contract change.** Handle/Vertex/Incidence/PropertyKey untouched; both invariants
  intact. The revision and the log are kernel *bookkeeping*, not primitives.

### 4.1 The honest exception — this spec does touch the concurrency model

`ALGORITHM_SPEC.md` §8 promised "no concurrency-model changes," and every algorithm since has been
read-only over `IHypergraphQuery`. **This one is different, and pretending otherwise would be the
wrong way to start.** A change log is the first piece of Topos state that is *written by the writer
thread and read concurrently as a queue* rather than as a point lookup.

It does not overturn Single-Writer/Multi-Reader — the writer is still one thread, readers are still
concurrent. But it adds a new shape of shared state to that model, and §3.3's stress test plus
§3.4's benchmark exist precisely because that claim needs evidence rather than assertion.

---

## 5. The implementation spec

### 5.1 Revision counter (Kernel) — MUST

```csharp
namespace Topos.Hypergraph;

public sealed partial class HypergraphKernel
{
    /// <summary>
    /// Monotonic count of writes applied to this kernel. Starts at 0, increments by exactly one per
    /// write. Two reads returning the same value mean nothing changed in between; a higher value
    /// means at least one write landed. Never decreases, never wraps in any realistic lifetime
    /// (long).
    /// </summary>
    public long CurrentRevision { get; }
}
```

- **Implementation:** one `long` field, incremented inside each of the seven write methods. Read
  via `Volatile.Read` so a reader thread observes it promptly.
- **Complexity:** O(1), one increment per write. This is the item with the best
  value-to-risk ratio in the whole spec and could ship alone.
- **Tests:** `RevisionTests` — exactly-once per write method (all seven, table-driven), monotonic
  across a mixed sequence, unchanged by read-only calls.

### 5.2 Change log + `ChangesSince` (Kernel) — MUST

```csharp
namespace Topos.Hypergraph;

/// <summary>What kind of change touched a handle.</summary>
[Flags]
public enum ChangeKind
{
    None = 0,
    VertexCreated  = 1 << 0,
    StatusChanged  = 1 << 1,   // SetDormant / Reactivate
    IncidenceAdded = 1 << 2,
    PropertySet    = 1 << 3,
    PropertyRemoved= 1 << 4,
}

public readonly record struct GraphChange(long Revision, Handle Handle, ChangeKind Kind);

public sealed partial class HypergraphKernel
{
    /// <summary>
    /// Every change recorded since <paramref name="revision"/>, oldest first. Empty when nothing
    /// has changed. <paramref name="overflowed"/> is true when the log has discarded changes the
    /// caller had not yet seen — the caller's cursor is stale and a full rescan is required; the
    /// returned changes are then a suffix, not the whole story.
    /// </summary>
    public IReadOnlyList<GraphChange> ChangesSince(long revision, out bool overflowed);
}
```

- **Implementation:** a fixed-capacity ring of `GraphChange` behind the same
  `ReaderWriterLockSlim` pattern every other kernel pool uses (§3.4's rationale). Appends take the
  write lock the writer thread already holds conceptually; `ChangesSince` takes the read lock and
  copies out.
- **Overflow detection:** the ring records the oldest revision it still holds. If
  `revision < oldestRetained`, set `overflowed` and return what remains. This must be impossible to
  miss — hence an `out` parameter rather than a nullable field a caller can ignore.
- **Complexity:** O(1) amortized per write; `ChangesSince` is O(changes returned).
- **Tests:** `ChangeLogTests` (T4 hand-computed sequences), `ChangeLogPropertyTests` (§3.2's eight
  properties), `ChangeLogDiffParityTests` (§3.1's brute-force oracle),
  `ChangeLogConcurrencyTests` (§3.3's stress test).

### 5.3 Incidence mutability — MUST (a decision; possibly zero code)

Today there is **no `RemoveIncidence`** anywhere in the kernel — `AddIncidence` has no counterpart,
so a hyperedge's membership is grow-only: once a vertex participates in a relation, it participates
forever. `[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:120]`

This is very likely deliberate (it preserves provenance, and it composes with dormancy and with
M12's chains into a supersede-and-tombstone idiom). **But it is nowhere documented as an
invariant.** `CONCEPTS.md` states Invariant 1 about *vertices* — "a vertex, once created, is never
removed" — and says nothing about incidences, so a reader reasonably concludes membership is
editable. `[verified:docs=docs/CONCEPTS.md:126]` The M12 decision entry also lists "evolving
hyperedge membership" among the wishlist items already covered by existing primitives, which is
true only in the supersede sense, not literally. `[verified:docs=docs/DECISIONS.md 2026-09-06]`

See §6.5. Whichever way it goes, the outcome is a documented invariant and, if removal is admitted,
one more `ChangeKind`.

---

## 6. Open forks for Nasser (🟡 — your calls gate scope)

### 6.1 Is the change log always on, or opt-in?

- **(a) Always on, fixed capacity.** Simplest to reason about; every consumer can rely on it. Costs
  every write a ring append forever, including workloads that never read it.
- **(b) Opt-in** — `kernel.EnableChangeLog(capacity)`, off by default. Zero cost for existing
  consumers (RLB, NexusVerifier, ChatMemory), which is most of them today.

**Recommend (b)**, with the revision counter (§5.1) always on regardless — the counter is a single
increment and genuinely free, while the log is the part with a real per-write cost. That split lets
every consumer cheaply ask "did anything change?" and only those that need detail pay for it.

### 6.2 Change granularity: coalesced per handle, or one record per operation?

- **(a) One record per write operation.** Faithful; a handle written 50 times yields 50 records,
  which can flood a bounded ring from one hot vertex.
- **(b) Coalesced per handle since the cursor** — one record per handle with `ChangeKind` flags
  OR-ed together. Bounded by the number of *distinct* handles touched, not by write volume.

**Recommend (b).** The question a consumer actually asks is "what do I need to re-read," and that is
answered by the set of touched handles. It also makes the §3.1 diff oracle exact rather than
approximate, because a brute-force snapshot diff is inherently coalesced — it cannot see that a
property was written five times. Choosing (a) would mean the oracle can only ever check a weaker
claim, which is a real verification cost for a fidelity nobody has asked for.

### 6.3 Bounded ring, or unbounded log?

- **(a) Bounded ring with loud overflow.** Memory is capped; a slow reader is told to rescan.
- **(b) Unbounded.** No reader ever misses anything — and an unbounded buffer in a long-lived
  process is an unbounded leak, in a project whose §2 already rules out "grows forever" designs.

**Recommend (a).** Note that under §6.2(b) coalescing, the ring is sized in *distinct handles*, so a
modest capacity covers far more write volume than it looks like it would.

### 6.4 Where does the consumption API live — kernel, or `Topos.Hypergraph.Living`?

- **(a) Kernel.** `ChangesSince` sits on `HypergraphKernel` next to the state it reports on. One
  package; no consumer needs a second dependency to notice that the graph moved.
- **(b) `Topos.Hypergraph.Living`.** Thematically where "living graph" belongs, and keeps the kernel
  surface smaller. But the storage has to be in the kernel regardless (§4), so this splits one
  feature across two packages with an internal seam between them.

**Recommend (a)** for the API, while framing the whole capability as *Living* in the docs. The
naming question and the packaging question are separable, and only the second one has a cost.

### 6.5 Is incidence immutability an invariant, or a gap? (§5.3)

- **(a) Declare it an invariant.** "An incidence, once created, is never removed" becomes Invariant
  3, documented in `CONCEPTS.md`/`SPECIFICATION.md` beside the existing two, with the
  supersede-and-tombstone idiom written up as the sanctioned way to revise a relation. Zero code.
- **(b) Add `RemoveIncidence`.** Makes relations editable in place, costs a new `ChangeKind`, and
  reopens a storage-contract question the architecture has treated as closed.

**Recommend (a)** — it matches what the code already does, it preserves the provenance property the
rest of the design leans on, and it is a documentation fix rather than an architectural change. But
this is exactly the kind of call that should be *made* rather than inherited by default, which is
why it is here and not assumed.

### 6.6 Does `RestoreVertex` bump the revision?

`RestoreVertex` exists for snapshot reload, not for live mutation.
`[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:80]` Reloading a 100k-vertex snapshot would
otherwise emit 100k "changes" that no consumer wants and that would instantly overflow any ring.

**Recommend: no** — reload is not a change, it is a new baseline. A reloaded kernel starts at
revision 0 with an empty log, and any consumer holding a pre-reload cursor gets an overflow signal.
The same reasoning applies to the MCP server's `reset_kernel`, which swaps the kernel instance
outright. `[verified:docs=docs/DECISIONS.md 2026-09-12]`

### 6.7 Pull only, or push as well?

**Recommend pull only for M13.** §2 already buckets push as good-to-have; stating it as a fork here
so the answer is on the record rather than implied by an omission.

---

## 7. Exit criterion for M13

Mirrors M9's and M11's falsifiable-gate discipline — with one change, for a reason.

1. **Revision counter and `ChangesSince` implemented**, with §3.1's brute-force diff oracle,
   §3.2's eight properties, §3.3's concurrency stress test, and §3.4's benchmarks all passing.
2. **Writer-side cost measured and published** in `docs/` — the per-write overhead with the log
   enabled and with it disabled. A number, not a claim.
3. **Incidence mutability decided and documented** (§6.5) — a `DECISIONS.md` entry either way.
4. **One real consumer replaces a hand-rolled poll-and-diff with the change feed.**

**Item 4 is not deferrable, and that is the change.** M11's exit criterion contained the same
requirement — two real consumers exercising the new surface — and it has now been deferred at phase
1 (2026-07-30), at the 08-03 follow-up, and again at phase 2 (2026-09-16), while M12 shipped without
one too. `[verified:docs=docs/DECISIONS.md]` Three milestones of capability have been verified for
*correctness* and never once for *fitness*. A change-feed API is especially exposed to that failure:
its whole value is ergonomic — whether a consumer can actually keep something in sync with less code
than it takes to rescan — and no oracle, property or benchmark can tell you that. **If item 4 is
going to be deferred again, this milestone should not start.**

---

## 8. What this spec does NOT propose (scope boundaries)

- **No storage-contract change.** Handle/Vertex/Incidence/PropertyKey untouched; both invariants
  intact. (§6.5 may *add* a documented invariant; it changes no stored shape.)
- **No MVCC, no time-travel reads, no rollback.** Readers see current state, as today.
- **No second history mechanism.** `Living` owns semantic history; `Persistence` owns durable
  state; the change feed owns "what moved recently" and nothing more (§2's NO-need).
- **No cross-process delivery.**
- **No new algorithms.** This milestone adds no query surface.

---

## 9. Sources

- The gap, established by direct reading this session, not inherited from prior docs:
  `src/Topos.Hypergraph/HypergraphKernel.cs` (write surface, concurrency model),
  `HypergraphViews.cs` (live views, the handle-index cursor trick),
  `IHypergraphQuery.cs` (`VertexHandles` snapshot semantics).
- Benchmark precedent for §3.4: `docs/M0_BENCHMARK_RESULTS_2026-07-24.md` §2, §3, §5 — the COW
  incidence index that lost to the boring `ReaderWriterLockSlim` design.
- M12's scope and its wishlist: `docs/DECISIONS.md` 2026-09-06.
- Verification ladder (T1–T6, and the novel-by-nature argument this spec's §3 applies):
  `docs/ALGORITHM_GAP_LIST.md` §3-4.
- Document shape and fork discipline: `docs/ALGORITHM_SPEC.md`.
