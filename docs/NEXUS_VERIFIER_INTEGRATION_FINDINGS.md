# Findings from the NexusVerifier integration — handoff to Opus 5 (M8/M9)

**Author:** GLM-5.2 (the agent that did the NexusVerifier ↔ Topos integration, July 2025)
**Audience:** Opus 5, before starting M8 (API stability / OSS polish) and M9
**Purpose:** Transfer everything I learned from *using Topos against a real second consumer*
(NexusVerifier's AND-OR proof-search backward chainer) so it feeds directly into the API-stability
review. **Every finding below came from source-level integration work, not inference.** Citations
are `file:line` against the current `main` of this repo unless marked `[ NexusVerifier ]` or
`[ RLB ]` (those point at the other repos and are for context only).

The full integration writeup lives in the NexusVerifier repo at
`docs/TOPOS_INTEGRATION_REPORT.md` (branch `topos-integration`). This document is the
**Topos-side digest** — the subset that should change what M8 locks, documents, or fixes.

---

## TL;DR — what M8 should do with this

Six findings. Two are real bugs/gaps to fix, three are API-ergonomic improvements, one is a
documentation cleanup. Ordered by leverage:

| # | Finding | Severity | M8 action |
|---|---|---|---|
| 1 | Per-Incidence cell properties are documented but not built | **medium — doc-vs-code lie** | Either build it or fix the doc. The doc currently promises an API that doesn't exist. |
| 2 | `Handle.Invalid` is a known gap; `Generation` is "load-bearing for nothing" | **low-medium — M8 is the moment to decide** | M8 is the API-freeze milestone. Either wire `Handle.Invalid` into `TryGet`-style failures now, or commit to the `default(Handle)` convention and remove the "known gap" hedge. Same for `Generation`. |
| 3 | No role registry / typed-role convention | low — ergonomic | Consider a layer-1 `AddIncidence<TRole>` overload or a documented `enum Role : byte` convention. |
| 4 | `IHypergraphQuery.HasCycle` misleads on n-ary graphs | low — already documented | Amplify the doc; consider renaming or moving to a role-aware layer-1 API. |
| 5 | No role-aware traversal at the kernel layer | low — by design | Confirm the layer-1 directed-search story is on the M8/M9 list. |
| 6 | (RLB-side, not Topos) `IGraphMemory` interface ergonomics for hyperedge-only backends | informational | Not your bug, but it shaped finding #3's urgency. |

**The integration also validated things that work well** — read the "What worked" section before
assuming the kernel needs wholesale changes. The n-ary rewrite in particular exercised Topos
exactly the way the spec hopes it'll be used, and it held up.

---

## 0. What the integration was (one paragraph of context)

NexusVerifier has a C# agent layer (`NexusAgent/`, .NET 10 — same runtime as Topos) that does
AND-OR backward-chaining proof search over a tactic-application graph. I built two chain paths
that both use Topos as the storage backend:

- **Projected path:** stores the tactic graph natively n-ary in a Topos `HypergraphKernel`, then
  projects to Rich-Learning-Base V2's `HyperEdge` (1 Anchor + N Conditions + 1 Target) at the
  boundary so the existing chainer runs unmodified. Tests Topos as a clean storage backend.
- **N-ary path:** the chainer reads Topos's genuine n-ary shape directly — `GetVertexHyperedges`
  → `IncidencesFrom` → role filter. No projection, no V2 types in the hot path. Credit
  assignment reinforces a Topos `LearnableEdge` property via `GetProperty → Reinforce → SetProperty`.
  This is the path that actually exercises Topos the way the spec intends.

**Both paths produce identical search outcomes** on the same fixture (pinned by parity tests).
22 tests pass. Topos itself is **completely unchanged** — every finding below is a candidate for
M8, nothing was patched in the integration.

The domain is a proof-search hypergraph: one Edge-vertex per `(goal, tactic)` group, with the
goal as a before-role member (ordinal 0) and each subgoal the tactic produces as an after-role
member (ordinal 1..N). A tactic producing 2 subgoals is one Edge-vertex with 3 incidences — the
genuine n-ary fact, no contortion.

---

## 1. Per-Incidence cell properties are documented but not built  *(the real bug)*

**Severity: medium. This is a doc-vs-code lie that a real consumer tripped over.**

### The claim

`src/Topos.Hypergraph/Incidence.cs:15-19` (the class-level doc on `Incidence`) says:

> *Cell-level properties (theta, confidence, transition counts) attach to the (Source, Member,
> Ordinal) triple via `PropertyKey<T>` pools keyed on the member Handle within that edge's scope —
> the mechanism for this lands with M2's reification work; M0 only stores the membership shape
> itself.*

### The reality

M2 shipped nested reification only (edges-as-members at depth N — see
`tests/Topos.Hypergraph.Tests/ReificationTests.cs`). The per-(Source, Member, Ordinal) addressing
mechanism the doc promises **was not built**. `HypergraphKernel.SetProperty<T>` keys on a single
`Handle` (`src/Topos.Hypergraph/HypergraphKernel.cs:131`):

```csharp
public void SetProperty<T>(PropertyKey<T> key, Handle handle, T value)
```

There is **no overload taking an `Incidence` or a `(source, member)` pair** anywhere in the kernel.
`PropertyPool<T>` (`src/Topos.Hypergraph/PropertyPool.cs`) is a `SparseSet<T>` keyed solely on
`Handle`.

### What the consumer (me) had to do

I wanted to record per-subgoal success rates — "this tactic, applied to this goal, produced *this
specific subgoal* with success-rate X." That's a cell-level property in the spec's framing. I had
to fall back to attaching the stats to the **edge-vertex** (the `samples/Topos.Samples.ChatMemory/
ChatMemory.cs` `RecordRecallFeedback` pattern), losing per-subgoal granularity. Functional, but
the documented one-call mechanism doesn't exist, and a consumer reading the doc would expect
`kernel.SetProperty(key, incidence, value)` and find no such API.

### M8 options (in order of how I'd lean)

1. **Fix the doc to match the code.** Strike the "lands with M2's reification work" promise,
   replace with an honest "per-incidence properties are a layer-1 concern; the kernel stores
   properties per-vertex. Consumers needing cell-level data either reify each membership as its
   own edge-vertex (M2-supported) or keep a side index." Cheapest, and matches what
   `ChatMemory` + my integration actually do.
2. **Build the mechanism.** Add `SetProperty<T>(PropertyKey<T>, Incidence, T)` backed by a
   pool keyed on `(source.Index, member.Index, ordinal)` or a reified cell-handle. Higher cost,
   real value only if a forcing consumer needs it — and right now two consumers (ChatMemory,
   NexusVerifier) worked around it fine.

I'd default to option 1 unless you have a third consumer in mind that genuinely needs per-cell
addressing. The workarounds are clean.

---

## 2. `Handle.Invalid` and `Generation` — M8 is the decision moment  *(the API-freeze decision)*

**Severity: low-medium. Not bugs, but exactly the kind of thing M8 exists to lock.**

### The current state (both honestly documented, which is good)

`src/Topos.Hypergraph/Handle.cs`:

- **`Handle.Invalid`** (lines 17–28): a reserved sentinel (`new(uint.MaxValue, uint.MaxValue)`)
  that **the kernel's own `TryGet`-style failures do not use** — they return `default(Handle)`
  (Index 0) instead. The doc explicitly calls this a "known gap rather than silently wired in"
  because switching to `Invalid` would be a behavioral change, not a doc fix. Only
  `IHypergraphQuery.HasCycle`'s internal DFS uses `Invalid` (as the "no parent" root sentinel).

- **`Generation`** (lines 9–14): "reserved for M4 physical-slot-relocation detection... in M0
  (in-memory, no compaction) it is always 0 and **load-bearing for nothing** — the field exists
  now so the struct layout is stable through M4's compaction addition."

### Why M8 is the moment

M4 has shipped (tiered persistence — `src/Topos.Hypergraph.Persistence/`). M8 is the API-stability
milestone. These two hedges have been carried since M0, and **the whole point of M8 is to stop
carrying hedges** — lock the convention, commit to the wire format, document the semantics.

### The decisions to make

- **`Handle.Invalid`:** Either (a) wire it into `TryGetVertex`/`TryGetProperty`/etc. as the
  failure value (cleaner for consumers — `default(Handle)` collides with real vertex #0), or
  (b) commit to the `default(Handle)` + `bool` out-param convention and **delete the `Invalid`
  sentinel** (or document it as internal-only). Don't keep both. My integration didn't trip over
  this because I always checked the `bool`, but it's a footgun for the next consumer.

- **`Generation`:** M4 shipped without using it (snapshot persistence restores Handles verbatim,
  no compaction/relocation). So either (a) M8 is when you add the compaction pass that makes
  `Generation` load-bearing (probably out of scope for an API-stability milestone), or (b) you
  commit to "Generation stays reserved, here's the documented contract for when something uses
  it." Pick (b) for M8 unless compaction is secretly already planned — and if you pick (b),
  update the doc to say "reserved for a future compaction milestone, not M4" rather than the
  stale "lands with M4" framing.

The honest read: these have been "TODO" since M0. M8's job is to convert TODOs to decisions.

---

## 3. No role registry / typed-role convention  *(ergonomic)*

**Severity: low. Workable today, but every consumer reinvents the same boilerplate.**

### The current state

`HypergraphKernel.AddIncidence(Handle source, Handle member, byte role, int ordinal)`
(`src/Topos.Hypergraph/HypergraphKernel.cs:105`) takes `role` as a **raw `byte`**. The kernel
does not interpret it. `Incidence.cs:8-10` is explicit that this is by design:

> *Domain meaning (e.g. RLB's Anchor=0/Condition=1/Target=2) lives in the layer-1 Knowledge
> model... The kernel records; it does not judge.*

The only role enum in the kernel is `VertexRoles` (`src/Topos.Hypergraph/VertexRoles.cs`), which
is vertex-level and has exactly two values (`None`, `Edge`).

### What consumers actually do

Every consumer I looked at — `samples/Topos.Samples.ChatMemory/ChatMemory.cs:31` and my
`NexusAgent.ToposExperiment.Ingest.ToposAppliesAdapter` — defines its own `const byte` role
convention:

```csharp
// ChatMemory
private const byte SpeakerRole = 0, MentionedRole = 1, DerivedFromRole = 1, ...;

// My NexusVerifier adapter
public const byte BeforeRole = 0;  // V2 "Anchor"
public const byte AfterRole  = 1;  // V2 "Condition"
```

No compile-time help, no registry, no way to detect a collision if two consumers share a kernel.

### M8 consideration

The "kernel does not judge" principle is sound and I don't think you should violate it. But a
**layer-1 convention** (not kernel) would reduce per-consumer boilerplate:

- Option A: a thin `AddIncidence<TRole>(source, member, TRole role, int ordinal)` where `TRole :
  unmanaged, Enum` and the kernel converts to byte. Zero runtime cost, compile-time role typing.
- Option B: a documented `enum DomainRole : byte { ... }` extension pattern in a layer-1
  `Topos.Hypergraph.Knowledge` namespace, with no kernel change.
- Option C: do nothing, keep it a free byte. Defensible — both current consumers managed fine.

I'd lean B or C for M8 (low risk, no kernel API churn). Option A is nice but adds a generic method
to the hot path; only worth it if profiling says the cast is free (it almost certainly is, but
verify).

---

## 4. `IHypergraphQuery.HasCycle` misleads on n-ary graphs  *(documentation amplification)*

**Severity: low. Already documented honestly; a real consumer considered it and avoided it
correctly only because the doc was honest. Worth amplifying.**

### The current state

`src/Topos.Hypergraph/IHypergraphQuery.cs:252-263` (the `HasCycle` doc) and lines 318-326 (the
SCC doc) explain: any hyperedge with 3+ members is *trivially cyclic* under the clique-style
adjacency model `HasCycle` uses, so it returns true for nearly any real n-ary graph. The doc
explicitly says the useful question ("cyclic *dependency* among Anchor→Target legs") "needs
role-aware traversal — a layer-1 concern, not this method's job."

### The consumer experience

I considered using `HasCycle` for AND-OR cycle detection, read the doc, and correctly backed off
(my chainer implements its own per-DFS-path `HashSet<string>` cycle guard). **The doc saved me
from a bug.** That's a win for the project's documentation culture, not a problem.

### M8 consideration

Amplify the doc — maybe rename `HasCycle` to `HasTopologicalCycle` or add a `[Obsolete("...use
role-aware cycle detection at layer 1...")]` if you want to be aggressive. The current name reads
like a general-purpose cycle check and isn't. Either rename or make the caveat louder.

---

## 5. No role-aware traversal at the kernel layer  *(layer-1 gap, by design)*

**Severity: low. By design ("kernel does not judge"), but worth confirming the layer-1 story.**

### The current state

All kernel-level traversal in `IHypergraphQuery` (`src/Topos.Hypergraph/IHypergraphQuery.cs`) —
`GetBfs`, `GetDfs`, `GetShortestPath`, `GetConnectedComponents`, `HasCycle` — is **role-blind and
co-membership-symmetric**. `GetBfs` from a subgoal walks back into the tactic-edge and out to
sibling subgoals, because it doesn't know Anchor→Target directionality.

Lines 99-104 and 252-263 are explicit that role-aware traversal is "a layer-1 concern."

### The consumer experience

My n-ary chainer hand-rolls role-filtered walks over `GetVertexHyperedges` + `IncidencesFrom` +
`Incidence.Role` filtering — exactly the `samples/Topos.Samples.ChatMemory/ChatMemory.cs:81-85`
(`EntitiesMentionedIn`) pattern. It's ~10 lines of LINQ. Functional, but every directed/role-gated
consumer reinvents it.

### M8/M9 consideration

This is the biggest *layer-1* gap, not a kernel gap. The spec's "kernel does not judge" principle
is correct and I don't think the kernel should grow role-aware traversal. But if M9 includes a
layer-1 `Topos.Hypergraph.Knowledge` (or similar) package, **role-aware directed search is the
most obvious thing to put in it** — `DirectedBfs(start, roleFilter)`, `RoleGatedReachability`,
etc. Two consumers (ChatMemory, NexusVerifier) have now written the same hand-rolled version.

---

## 6. `IGraphMemory` interface ergonomics  *(RLB-side finding, informational)*

**Not a Topos bug. Recorded because it directly shaped finding #3's urgency and because Opus 5
may encounter it if M8 touches any cross-project convention.**

### The finding

Rich-Learning-Base's `IGraphMemory` (`~/projects/rich-learning-base/src/RichLearning.V2/
Abstractions/IGraphMemory.cs`) mixes **abstract "core" members** (lines 38-107: ~11
transition/landmark/pathfinding methods that have no default implementations and *must* be
implemented) with **default-implemented hyperedge members** (lines 114-136: added in M4, fall
through to `NotSupportedException`/empty/null). A hyperedge-only backend like my `ToposGraphMemory`
has to provide explicit `NotSupportedException` bodies for 11 methods the chainer never calls.

The M4 hyperedge default-implementation pattern should have been applied retroactively to the core
members when M4 added hyperedges. It wasn't, and now any hyperedge-only backend pays the
boilerplate cost.

### Why it matters to Topos

It's a lesson in **interface design for a library that grows capabilities over time**: when you add
a new capability (hyperedges) with a default-implementation escape hatch, the older capabilities
don't automatically get the same treatment, and backfilling them is breaking. If Topos ever grows
a similar "optional capability" split in a layer-1 interface, learn from this — either everything
is default-implemented from day one, or nothing is.

---

## What worked well (read this before assuming the kernel needs work)

The integration wasn't a pile of complaints — most of Topos worked exactly as the spec hopes.
Specifically:

1. **The four-primitive contract is genuinely sufficient for an n-ary proof-search domain.** One
   Edge-vertex per tactic-application, N member incidences with role bytes + ordinals, typed
   properties on the edge-vertex. Zero kernel changes needed to express the whole domain. This is
   the spec's central claim, and it held.

2. **`LearnableEdge` + `EdgeStatistics` are exactly the right abstraction level** for a consumer
   that isn't RLB. `LearnableEdge` (`src/Topos.Hypergraph/LearnableEdge.cs`) generalizes RLB's
   `ThetaParameters` without RLB's fixed 7-slot layout — I used a 3-slot theta (bias + 2 features)
   and it just worked. `EdgeStatistics` similarly. M5's falsifiability gate is real.

3. **The n-ary path's read-modify-write over `LearnableEdge` (immutable value + kernel as source
   of truth) is a genuinely better pattern than V2's mutable `HyperEdge` + reference-stability
   contract.** The V2 path had a silent-failure mode where a backend that defensively copied on
   read would break credit assignment with no error. The Topos path can't have that bug because
   there's no shared mutable object. **If M8 is locking API patterns, the immutable-value +
   kernel-as-truth pattern is the one to bless** — document it as the recommended way to do
   learnable state, and discourage consumers from holding mutable references across kernel calls.

4. **Reference stability through the kernel is automatic.** The projected path needed a careful
   reference-stability contract (cache `HyperEdge` instances, return the same one per id). The
   n-ary path gets it for free because there's one kernel object. This is a real ergonomic win
   for the kernel-as-truth pattern.

5. **`HypergraphKernel`'s public API is small and coherent** (21 public members). I never found
   myself wanting a method that didn't exist (cell properties aside), and I never had to reach
   for internals. `InternalsVisibleTo` was used only for tests, not the integration itself.

6. **The doc-comment culture paid off repeatedly.** `Handle.cs`'s "known gap" note, `Incidence.cs`'s
   "kernel does not judge" framing, `IHypergraphQuery.cs`'s `HasCycle` caveat — all of these
   *correctly shaped my integration decisions* before I hit the code. Keep this standard. The one
   place it slipped is finding #1 (the cell-property doc promises something that doesn't exist) —
   that's the outlier, and fixing it (or the code) keeps the standard intact.

---

## Concrete artifacts Opus 5 can look at

If it helps to see exactly what a real consumer did:

- **The integration report (full):** `~/projects/NexusVerifier/docs/TOPOS_INTEGRATION_REPORT.md`
  (branch `topos-integration`). The §2.5 "n-ary chainer rewrite" section is the one that
  exercises Topos the way the spec intends.
- **The n-ary chainer reading Topos natively:**
  `~/projects/NexusVerifier/NexusAgent/NexusAgent.ToposExperiment/NarySearch/NaryBackwardChainer.cs`
  — a ~350-line example of consuming the kernel's public API for role-aware directed search.
- **The credit-assignment pattern I'm recommending M8 bless:**
  `~/projects/NexusVerifier/NexusAgent/NexusAgent.ToposExperiment/NarySearch/ToposNativeCreditAssignment.cs`
  — `GetProperty → Reinforce → SetProperty` over an immutable `LearnableEdge`.
- **The adapter that builds the n-ary graph:**
  `~/projects/NexusVerifier/NexusAgent/NexusAgent.ToposExperiment/Ingest/ToposAppliesAdapter.cs`
  — the `BuildNaryAsync` method is the cleanest "external domain built purely on public API"
  example after `ChatMemory`.
- **The test that pins correctness parity between the two paths:**
  `~/projects/NexusVerifier/NexusAgent/NexusAgent.ToposExperiment.Tests/NaryChainerTests.cs` —
  `NaryProjectedParityTests` proves the n-ary and projected chainers produce identical search
  outcomes.

---

## One non-finding worth flagging

The NexusVerifier integration did **not** exercise: spectral anything (M7, deferred by design — no
forcing requirement), tiered persistence under real load (M4 — my synthetic fixture is tiny), the
GDS-oracle harness (M1 — that's a Topos-internal concern, not a consumer concern), or anything
concurrent (SWMR — my harness is serial like the V2 experiment it mirrors). So this document has
nothing to say about those areas. If M8/M9 touches them, this isn't the reference; the existing
`docs/` and tests are.

The integration *did* exercise: the four-primitive storage contract, `LearnableEdge`/`EdgeStatistics`
as M5's generalizable learning substrate, the public read/traversal API, the typed-property-pool
pattern, and the kernel-as-source-of-truth mutation pattern. Those are the areas the findings above
cover.

---

## Final note for Opus 5

The engineering under Topos is genuinely solid — measured benchmarks, real GDS verification, two
real consumers now. My integration found one real doc-vs-code bug (#1), a couple of API-freeze
decisions that have been deferred since M0 and really do belong to M8 (#2), and some ergonomic
notes (#3-5). Nothing here is a structural problem with the thesis or the implementation. The n-ary
path in particular is the cleanest use of Topos I've seen, and it worked first try once the code
compiled — which is the strongest validation the four-primitive contract could get.

If M8 does nothing else from this list, **fix finding #1** (the cell-property doc lie). It's the
only thing here that would actively mislead a future consumer. Everything else is improvement, not
repair.
