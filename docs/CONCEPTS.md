# Concepts — the Topos mental model

**Date:** 2026-07-26 · **Author:** GLM-5.2 (ZCode) · **Audience:** anyone — new contributor, new
agent, or new consumer — who needs to *understand* Topos before using it. This is the conceptual
entry point; the runnable walkthrough is [`GETTING_STARTED.md`](GETTING_STARTED.md), and the
per-type details are in [`API_REFERENCE.md`](API_REFERENCE.md).

---

## What Topos is, in one paragraph

Topos is an **embedded, in-process, typed-property hypergraph kernel for C#**, optimized for
long-lived adaptive symbolic memory — incremental updates, provenance, explainability, retrieval,
symbolic+vector coexistence, stable identities, mutable knowledge that grows rather than being
overwritten. `[verified:docs=docs/SPECIFICATION.md §2.1]` "Embedded, in-process" means like SQLite
or Kuzu: you reference it as a library and hold a `HypergraphKernel` object in your process, not a
server you connect to. "Hypergraph" means an edge (a *hyperedge*) can have **any number of member
vertices**, not just two — which is the whole reason this library exists instead of just using a
property graph. The full thesis and the workload argument for it live in
[`SPECIFICATION.md` §1](SPECIFICATION.md); this doc is about *how the thing is shaped*, not *why it
should exist*.

---

## The four primitives

Everything in Topos is built from four primitive types. They are the load-bearing storage contract;
spec §3 pressure-tested them across four review rounds and they survived every attempt to add a
fifth. `[verified:docs=docs/SPECIFICATION.md §3]` `[verified:docs=docs/DECISIONS.md §1]`

### 1. `Handle` — stable identity

```csharp
public readonly record struct Handle(uint Index, uint Generation = 0);
// src/Topos.Hypergraph/Handle.cs
```

A `Handle` is the stable identity of a vertex. `Index` is a **monotonic, never-reused counter** —
once a vertex exists, its Handle's logical identity never changes and is never recycled, even after
the vertex goes dormant (more on that under Invariant 1 below). `[verified:src=src/Topos.Hypergraph/Handle.cs:6-11]`

`Generation` is a reserved field for a possible future physical-slot-compaction feature; it is
always `0` today and load-bearing for nothing. The field exists so the struct layout is stable if
that future milestone ever needs it. `[verified:src=src/Topos.Hypergraph/Handle.cs:11-15]`

Two sentinels worth knowing: `Handle.Invalid` (the "no vertex" marker, `new(uint.MaxValue, uint.MaxValue)`)
and `Handle.IsValid` (a convenience check). Failure paths on `TryGetVertex` set the out `Vertex`'s
`Handle` to `Invalid` rather than C#'s `default(Handle)` — which would otherwise be indistinguishable
from a real vertex #0. Always check the `bool` returned by `TryGetVertex`; `IsValid` is
defense-in-depth, not the primary contract. `[verified:src=src/Topos.Hypergraph/Handle.cs:19-36]`

### 2. `Vertex` — the node record

```csharp
public readonly record struct Vertex(Handle Handle, VertexRoles Roles, VertexStatus Status)
{
    public bool IsDormant => Status == VertexStatus.Dormant;
}
// src/Topos.Hypergraph/Vertex.cs
```

A `Vertex` is the node record. Notice what is **not** here: no data field, no dictionary of
properties. Typed data attaches separately via `PropertyKey<T>` pools (primitive 4 below). The only
things on the record itself are `Handle`, `Roles`, and `Status` — and those three sit inline on the
record *because they are read on every traversal hop*; a property-bag indirection in that inner loop
would be the wrong tier. `[verified:src=src/Topos.Hypergraph/Vertex.cs:3-8]`

- **`VertexRoles`** (`[Flags] enum : byte`) — kernel-level roles only. Today there is exactly one:
  `VertexRoles.Edge`, marking a vertex as a *reified hyperedge* (see "Hyperedges as reified vertices"
  below). Domain-specific roles (RLB's Anchor/Condition/Target, your domain's equivalents) do **not**
  live here — see "Roles vs. VertexRoles" below. `[verified:src=src/Topos.Hypergraph/VertexRoles.cs]`
- **`VertexStatus`** (`enum : byte`) — `Active` or `Dormant`. Read per-hop to skip dormant vertices
  during traversal. `[verified:src=src/Topos.Hypergraph/VertexStatus.cs]`

### 3. `Incidence` — one membership

```csharp
public readonly record struct Incidence(Handle Source, Handle Member, byte Role, int Ordinal);
// src/Topos.Hypergraph/Incidence.cs
```

An `Incidence` is **one membership** in a hyperedge: `Member` participates in `Source` under `Role`
at position `Ordinal`. This is the primitive that makes the hypergraph n-ary: one hyperedge is just
the collection of all incidences sharing a `Source`. `[verified:src=src/Topos.Hypergraph/Incidence.cs:3-5]`

- **`Source`** is the Handle of a vertex tagged `VertexRoles.Edge`.
- **`Member`** is a participant vertex.
- **`Role`** is a raw `byte`. The kernel does not interpret it. Domain meaning lives in your layer-1
  code (see "Roles vs. VertexRoles" below). The recommended way to define role bytes is a
  `byte`-backed `enum` — see [`ROLE_CONVENTIONS.md`](ROLE_CONVENTIONS.md).
- **`Ordinal`** is an `int` position. Useful when order matters (e.g. "the Anchor is ordinal 0,
  the Target is the last member").

### 4. `PropertyKey<T>` — typed data identity

```csharp
public readonly record struct PropertyKey<T>
{
    internal PropertyKey(string name, int id) { Name = name; Id = id; }
    public string Name { get; }
    public int Id { get; }
}
// src/Topos.Hypergraph/PropertyKey.cs
```

A `PropertyKey<T>` is the *typed identity* of a property, separate from its storage. `Name` is the
stable, human-facing identity (e.g. `"content"`, `"embedding"`); `Id` is a per-process integer slot
resolved once via `PropertyRegistry` and cached on the key for O(1) lookup. You do **not** construct
one directly — the constructor is `internal` (locked M8); the only way to get one is
`kernel.ResolveProperty<T>("name")`. `[verified:src=src/Topos.Hypergraph/PropertyKey.cs:6-26]`

Behind the scenes, each `(T)` lives in its own columnar pool — an EnTT-style sparse-set, where every
vertex with a value for that property sits in a dense parallel array. Columnar on purpose: iterating
"every value of property X" (the shape persistence and analytics want) is a tight loop over one
array, not a scatter-read across vertices. `[verified:docs=docs/SPECIFICATION.md §3]`

---

## The two invariants

The four primitives are governed by two invariants. These are not conventions — they are rules the
kernel enforces, and your code can rely on them. `[verified:docs=docs/SPECIFICATION.md §3]`

### Invariant 1 — dormant is never garbage-collected

> A vertex, once created, is never removed. Going `Dormant` tombstones it (it stops participating in
> new traversals) but it stays resolvable forever — including as a `Member` target of an `Incidence`.

The operational consequence: **provenance edges always resolve.** If you record "fact F was derived
from fact G," and later mark G dormant, the edge from F to G still resolves to G's `Vertex`. You
never get a dangling Handle. `[verified:src=src/Topos.Hypergraph/Incidence.cs:16-28]`

This is why `Handle.Index` is never reused: if it were, a stale Handle from an old provenance edge
could silently point at a *different* vertex after recycling. Monotonic allocation + dormant-never-GC
together make Handle identity a safe long-lived reference. `[verified:src=src/Topos.Hypergraph/HandleAllocator.cs:4-7]`

### Invariant 2 — `VertexRoles` and `Incidence.Role` are independent axes

A vertex has a kernel-level `VertexRoles` (today: `None` or `Edge`). A vertex's *participation* in a
hyperedge has a layer-level `Incidence.Role` byte (your domain's meaning). **These do not interact.**
A vertex tagged `VertexRoles.Edge` (itself a reified hyperedge) can participate as a member of
another hyperedge under any role byte — that's how nested reification works (see
[`USAGE_PATTERNS.md`](USAGE_PATTERNS.md)). `[verified:docs=docs/SPECIFICATION.md §3]`

---

## Roles vs. `VertexRoles` — the single most confusing fork

If you read one section twice, read this one. There are two completely separate "role" concepts and
they share a name:

| Concept | Type | Where | Who interprets it |
|---|---|---|---|
| **Kernel role** | `VertexRoles` (`[Flags] enum : byte`) | inline on `Vertex` | the kernel; `Edge` is the only member today |
| **Domain role** | `byte` (the `Role` field of `Incidence`) | on each `Incidence` | **your layer-1 code, never the kernel** |

The kernel-level `VertexRoles.Edge` marks "this vertex is itself a reified hyperedge." That's it.
The kernel has no concept of Anchor, Condition, Target, Speaker, Mentioned, Before, After — those
are all *domain* roles, carried as the raw `byte` on each `Incidence`, and interpreted by the layer
sitting on top of the kernel.

This split is the **"the kernel records; it does not judge"** principle from spec §4.1. The kernel
stores role bytes faithfully and indexes them for O(1) lookup; it does not validate cardinalities
(e.g. "exactly one Anchor") or attach semantics to specific byte values. Cardinality validation,
role-naming conventions, and any business rule about what a role means all live in layer-1 — either
in your consumer code or in the optional [`Topos.Hypergraph.Knowledge`](#the-layer-architecture)
package for the patterns multiple consumers have already needed.
`[verified:src=src/Topos.Hypergraph/Incidence.cs:6-15]` `[verified:docs=docs/SPECIFICATION.md §4.1]`

The recommended pattern for defining your domain's role bytes is a `byte`-backed `enum` — see
[`ROLE_CONVENTIONS.md`](ROLE_CONVENTIONS.md).

---

## Hyperedges as reified vertices

A hyperedge in Topos is **not a separate primitive**. It is a vertex tagged `VertexRoles.Edge`,
connected to its members by incidences where `Source` is the edge-vertex's Handle. This is the
"Role:Edge reification" pattern (spec §7 pattern 12). `[verified:docs=docs/SPECIFICATION.md §7]`

Concretely, to model "one conversation turn mentioning three entities" as a single n-ary
relationship:

```csharp
var turn     = kernel.CreateVertex();                  // a domain vertex
var kyoto    = kernel.CreateVertex();                  // a domain vertex
var nara     = kernel.CreateVertex();
var osaka    = kernel.CreateVertex();

var mention  = kernel.CreateVertex(VertexRoles.Edge);  // the hyperedge, reified as a vertex
kernel.AddIncidence(mention, turn,  (byte)Role.Speaker,  ordinal: 0);
kernel.AddIncidence(mention, kyoto, (byte)Role.Entity,   ordinal: 1);
kernel.AddIncidence(mention, nara,  (byte)Role.Entity,   ordinal: 2);
kernel.AddIncidence(mention, osaka, (byte)Role.Entity,   ordinal: 3);
```

One hyperedge, four members — not three separate binary edges. This is the concrete shape of spec
§1's thesis: a single utterance mentioning N things together is one atomic event, and the storage
substrate preserves that atomicity rather than fragmenting it. `[verified:src=samples/Topos.Samples.ChatMemory/ChatMemory.cs:71-79]`

Why reify edges as vertices, instead of having a separate edge concept? Because it makes edges
first-class: an edge can have properties (`SetProperty` works on any Handle, including an
edge-vertex's), it can participate in *other* hyperedges (nested reification — see
[`USAGE_PATTERNS.md`](USAGE_PATTERNS.md)), and it can be the target of a provenance edge. None of
that needs a special case.

---

## The layer architecture

Topos is a 3-layer substrate: a **Storage model** (the kernel primitives), a **Graph model** (the
algorithms over those primitives), and a **Knowledge model** (domain semantics). This is locked
(spec §4), and the namespace boundaries in the shipped packages mirror it.
`[verified:docs=docs/SPECIFICATION.md §4]` `[verified:src=src/Topos.Hypergraph/Topos.Hypergraph.csproj]`
`[verified:src=src/Topos.Hypergraph.Knowledge/Topos.Hypergraph.Knowledge.csproj]`

```
┌─────────────────────────────────────────────────────────────┐
│  Layer 1 — Knowledge model (domain semantics)               │
│  Your code, optionally + Topos.Hypergraph.Knowledge (M9)    │
│  Role meanings, cardinality rules, directed/role-gated walk │
├─────────────────────────────────────────────────────────────┤
│  Layer 2 — Graph model (algorithms)                         │
│  Topos.Hypergraph: IHypergraphQuery + BFS/DFS/shortest-     │
│  path/cycle/components/s-walk/community detection           │
├─────────────────────────────────────────────────────────────┤
│  Layer 3 — Storage model (the kernel)                       │
│  Topos.Hypergraph: Handle / Vertex / Incidence /            │
│  PropertyKey<T>, the 2 invariants, SWMR concurrency         │
└─────────────────────────────────────────────────────────────┘
```

The key discipline: **role-aware / directed traversal stays out of layer 2.** Every default algorithm
on `IHypergraphQuery` (`GetBfs`, `GetDfs`, `GetShortestPath`, `GetConnectedComponents`, `HasCycle`)
is **role-blind and co-membership-symmetric** — "the kernel does not judge" (spec §4.1). They treat
any two vertices co-incident on the same hyperedge as adjacent, with no notion of "Anchor→Target
direction." `[verified:src=src/Topos.Hypergraph/IHypergraphQuery.cs:44-56]`

That judgment lives in **layer 1** — either in your consumer code (a ~10-line role-filtered walk,
which three independent consumers have hand-rolled) or, since M9, in the optional
`Topos.Hypergraph.Knowledge` package, which offers `DirectedBfs` / `DirectedShortestPath` /
`RoleFilteredMembers` over the same `IHypergraphQuery` surface. See
[`API_REFERENCE.md`](API_REFERENCE.md#knowledge-toposhypergraphknowledge) for those.
`[verified:src=src/Topos.Hypergraph.Knowledge/DirectedTraversal.cs:10-16]`

Persistence (`Topos.Hypergraph.Persistence`) is a separate package that sits across these layers —
it serializes layer 3's storage to disk and back. See [`API_REFERENCE.md`](API_REFERENCE.md).

---

## Concurrency, in one paragraph

The kernel uses a **Single-Writer / Multi-Reader (SWMR)** model. Handle allocation is genuinely
lock-free (`Interlocked.Increment`); every other piece of state — the vertex table, the incidence
indexes, every property pool — is one sparse-set behind its own `ReaderWriterLockSlim`. Read methods
are always safe to call concurrently with the single writer; write methods assume a single-writer
thread (concurrent writes need external synchronization). This is a deliberate, benchmark-driven
correction: the original copy-on-write design measured 5–6× slower than naive and O(N²) on hub
vertices; the per-pool-lock design measured *faster* than naive and eliminated the pathology. Full
data in [`M0_BENCHMARK_RESULTS_2026-07-24.md`](M0_BENCHMARK_RESULTS_2026-07-24.md).
`[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:6-31]` `[verified:docs=docs/DECISIONS.md §3.4]`

---

## What Topos is *not*

Scope boundaries, to prevent creeping expectations (lifted from spec §2.2):

- **Not an extraction pipeline.** Topos stores and queries; it does not run LLMs to extract triples
  from text. You can build extraction on top; Topos does not require it.
- **Not a server / hosted product.** Embedded only, like SQLite/Kuzu. You hold a `HypergraphKernel`
  in-process.
- **Not a reasoning / entailment / belief-revision engine.** It stores `AssertionMode`
  (asserted/quoted/hypothesized) and `Provenance`, but it does not reason over them. No contradiction
  resolution, no truth maintenance, no logical entailment. Storing an `AssertionMode.Hypothesized`
  flag is storage; revising beliefs from it is *your* job. A related point that's easy to get wrong:
  **provenance (where a fact came from) and confidence (how sure we are) are orthogonal axes**, and
  both are already representable as properties — `AssertionMode` tracks provenance,
  `EdgeStatistics.Confidence` tracks confidence. Resist adding a vertex *subclass*
  (`HypothesisVertex`, `ImaginedVertex`, etc.) that collapses the two: it forces a type migration on
  promotion and duplicates machinery the property model already provides. A hypothesized,
  low-confidence, or internally-generated concept is a regular `Vertex` with the relevant properties —
  never a different kind of vertex. This is the same "the kernel records; it does not judge" principle
  from Roles vs. `VertexRoles` above, applied to epistemic state.
  `[verified:src=src/Topos.Hypergraph/AssertionMode.cs]` `[verified:src=src/Topos.Hypergraph/EdgeStatistics.cs]`
- **Not multi-language by FFI.** Pure C#. The whole point (per the project's history) is eliminating
  a cross-language boundary.

`[verified:docs=docs/SPECIFICATION.md §2.2]`

---

## Where to go next

- **Write your first program** → [`GETTING_STARTED.md`](GETTING_STARTED.md) — a runnable walkthrough
  from `new HypergraphKernel()` to directed traversal and snapshot save/load.
- **Look up a type** → [`API_REFERENCE.md`](API_REFERENCE.md) — every public type, grouped by
  layer/package.
- **Model a shape** → [`USAGE_PATTERNS.md`](USAGE_PATTERNS.md) — n-ary facts, reification, per-cell
  data, views, semantic recall, learnable edges, persistence, directed traversal.
- **Understand the thesis / why this exists** → [`SPECIFICATION.md` §1](SPECIFICATION.md).
- **See what's locked vs. open** → [`SPECIFICATION.md` §11](SPECIFICATION.md) (quick-reference table)
  and [`DECISIONS.md`](DECISIONS.md) (the full decision log).
