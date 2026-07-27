# Usage patterns

**Date:** 2026-07-26 · **Author:** GLM-5.2 (ZCode) · **Audience:** a developer who has read
[`CONCEPTS.md`](CONCEPTS.md) and [`GETTING_STARTED.md`](GETTING_STARTED.md) and wants to model a
specific shape. This is the opinionated companion to [`API_REFERENCE.md`](API_REFERENCE.md) — *how*
to combine the primitives for common agent-memory workloads, with a cross-ref to where each pattern
is used for real.

> Every pattern here is built **purely from the public API** (`Topos.Hypergraph`, optionally
> `Topos.Hypergraph.Knowledge`, optionally `Topos.Hypergraph.Persistence`) — no internal access, no
> kernel changes. That's not a constraint of this doc; it's the falsifiability standard the kernel
> itself holds to (see the M5 sample). `[verified:src=samples/Topos.Samples.ChatMemory/ChatMemory.cs:5-18]`

---

## Pattern 1 — N-ary facts as one hyperedge

**When to use:** whenever a single event jointly involves N participants and the *togetherness* is
load-bearing — one utterance mentioning several entities, one decision jointly gated by several
conditions, one transaction touching several accounts. **The whole reason Topos exists.**

The thesis (spec §1): a binary graph can encode the *topology* of an n-ary event as a star, but it
loses the *atomicity* — joint credit assignment, joint statistics, and the "all-conditions-present"
gate have no faithful home on per-leg edges. One hyperedge preserves all three.
`[verified:docs=docs/SPECIFICATION.md §1.2]`

```csharp
using Topos.Hypergraph;

var kernel = new HypergraphKernel();

// A turn that mentioned three entities — one atomic relationship, not three edges.
public enum MentionRole : byte { Speaker = 0, Entity = 1 }

Handle turn  = kernel.CreateVertex();
Handle kyoto = kernel.CreateVertex();
Handle nara  = kernel.CreateVertex();
Handle osaka = kernel.CreateVertex();

Handle mention = kernel.CreateVertex(VertexRoles.Edge);
kernel.AddIncidence(mention, turn,  (byte)MentionRole.Speaker, ordinal: 0);
kernel.AddIncidence(mention, kyoto, (byte)MentionRole.Entity,  ordinal: 1);
kernel.AddIncidence(mention, nara,  (byte)MentionRole.Entity,  ordinal: 2);
kernel.AddIncidence(mention, osaka, (byte)MentionRole.Entity,  ordinal: 3);
```

One hyperedge, four members — and "these three were mentioned *together*, in this one turn" is stored
structurally, not reconstructed from three separate edges. This is the exact shape of
`samples/Topos.Samples.ChatMemory.RecordMention`. `[verified:src=samples/Topos.Samples.ChatMemory/ChatMemory.cs:66-79]`

**Variation — directed n-ary (Anchor fires toward Target, gated by Conditions):** this is RLB's
`HyperEdge` shape, generalized. Same primitives, different role bytes. `[verified:docs=docs/SPECIFICATION.md §1.1]`

```csharp
public enum ChainerRole : byte { Anchor = 0, Condition = 1, Target = 2 }

Handle anchor    = kernel.CreateVertex();
Handle condition = kernel.CreateVertex();
Handle target    = kernel.CreateVertex();

Handle decision = kernel.CreateVertex(VertexRoles.Edge);
kernel.AddIncidence(decision, anchor,    (byte)ChainerRole.Anchor,    ordinal: 0);
kernel.AddIncidence(decision, condition, (byte)ChainerRole.Condition, ordinal: 1);
kernel.AddIncidence(decision, target,    (byte)ChainerRole.Target,    ordinal: 2);
```

---

## Pattern 2 — Reification: edges as vertices

**When to use:** when you need to say something *about* a relationship — attach properties to it,
link it into another relationship, or record its epistemic status.

Reification is already built into the kernel: a hyperedge *is* a vertex tagged `VertexRoles.Edge`.
So you can attach properties to it, link it into other edges, and treat it as first-class — no
special API.

### 2a — Epistemic mode (asserted / quoted / hypothesized)

```csharp
PropertyKey<AssertionMode> mode = kernel.ResolveProperty<AssertionMode>("mode");

// A fact extracted from a turn, marked as a candidate belief (not yet confirmed).
Handle fact = kernel.CreateVertex(VertexRoles.Edge);
kernel.SetProperty(mode, fact, AssertionMode.Hypothesized);

AssertionMode? m = kernel.TryGetProperty(mode, fact, out var v) ? v : null;
```

`AssertionMode` is a plain typed property — not a reserved Vertex field — so a missing value means
"no mode recorded," not "defaulted to Asserted." Interpretation is a layer-1 concern: Topos stores
the flag, your code decides what to do with it. `[verified:src=samples/Topos.Samples.ChatMemory/ChatMemory.cs:88-99]`

### 2b — Nested reification (structural provenance)

An edge can be a *member* of another edge — because an edge is just a vertex. This is how you record
"fact F was derived from facts G and H" as structural provenance, not just a string label.

```csharp
// Two source facts.
Handle factG = kernel.CreateVertex(VertexRoles.Edge);
Handle factH = kernel.CreateVertex(VertexRoles.Edge);

// A derived fact, linked back to its sources via Incidence.
public enum DerivationRole : byte { Derived = 0, Source = 1 }

Handle factF = kernel.CreateVertex(VertexRoles.Edge);
kernel.AddIncidence(factF, factG, (byte)DerivationRole.Source,  ordinal: 0);  // factG is a MEMBER here
kernel.AddIncidence(factF, factH, (byte)DerivationRole.Source,  ordinal: 1);
```

This nests to arbitrary depth — `ReificationTests.DepthN_ChainOfNestedEdges_EveryLevelRoundTrips`
exercises a depth-4 chain (edge1 → edge2(edge1) → edge3(edge2) → edge4(edge3)), and every level
resolves correctly via `IncidencesFrom`/`IncidencesOf`. The two incidence indexes (`_bySource` and
`_byMember`) are independent, so a vertex being an edge *and* a member of another edge don't
interfere. `[verified:src=tests/Topos.Hypergraph.Tests/ReificationTests.cs:28-57]`

> **Provenance has two flavors — use the right one.** `Provenance(Source, RecordedAt)` (a typed
> property) is for *leaf* provenance: where a fact came from *outside* the graph (a document, user,
> external system). Nested reification is for *structural* provenance: which other *in-graph* facts a
> fact was derived from. Use both together. `[verified:src=src/Topos.Hypergraph/Provenance.cs:10-16]`

---

## Pattern 3 — Per-membership ("cell") data without a kernel primitive

**When to use:** when you need data that varies per *membership* — per (source, member) pair — rather
than per vertex or per edge. Example: a confidence score that's different for each leg of a multi-leg
decision.

**There is no `SetProperty(key, incidence, value)` on the kernel, by decision (M8).** The two real
consumers that wanted per-cell data both got by without it. `[verified:src=src/Topos.Hypergraph/Incidence.cs:16-28]` Two workarounds:

### 3a — Reify the membership as its own edge-vertex

If the per-leg data is rich enough to deserve first-class status, make each membership a vertex:

```csharp
PropertyKey<double> legConfidence = kernel.ResolveProperty<double>("legConfidence");

// Reify the (decision, condition) membership itself as a vertex, attach data to it.
Handle leg = kernel.CreateVertex(VertexRoles.Edge);
kernel.AddIncidence(leg, decision,  (byte)ChainerRole.Anchor,    ordinal: 0);
kernel.AddIncidence(leg, condition, (byte)ChainerRole.Condition, ordinal: 1);
kernel.SetProperty(legConfidence, leg, 0.83);
```

This is "edges-as-vertices" applied one more level (Pattern 2b). Each membership is now a first-class
vertex with ordinary per-Handle properties.

### 3b — Keep a side index in your own code

If the per-leg data is just a lookup table, keep it outside the kernel:

```csharp
// Keyed on the (Source, Member, Ordinal) triple — whatever identifies the cell.
var legConfidence = new Dictionary<(Handle Source, Handle Member, int Ordinal), double>();

legConfidence[(decision, condition, 1)] = 0.83;
```

Both are the patterns real consumers already use; there's no kernel-level shortcut. Pick (a) when the
data deserves to participate in traversal/analytics (it becomes a real vertex), (b) when it's pure
side-state that the graph never needs to walk over.

---

## Pattern 4 — Composable views

**When to use:** to query a *slice* of a kernel without copying it, or to combine two kernels/views
set-algebraically. All views implement `IHypergraphQuery`, so every algorithm works over them
unchanged.

### 4a — Subgraph / mask

```csharp
using static Topos.Hypergraph.HypergraphViews;

// A live view of only the active (non-dormant) vertices — re-evaluated on every call.
IHypergraphQuery activeView = Mask(kernel, h =>
    kernel.TryGetVertex(h, out var v) && v.Status == VertexStatus.Active);

// BFS over the masked view — sees only active vertices.
foreach (var v in activeView.GetBfs(start)) { /* ... */ }
```

`Subgraph` and `Mask` are the same mechanism (a `FilteredView`) under two names — a fixed vertex-set
and a live predicate are the same operation at different points on one spectrum. A member outside the
view is silently dropped from an edge's reported membership (JGraphT `AsSubgraph` convention), not an
error. `[verified:src=src/Topos.Hypergraph/FilteredView.cs:11-23]`

### 4b — Union / intersect / difference

```csharp
IHypergraphQuery both     = Union(viewA, viewB);
IHypergraphQuery shared   = Intersect(viewA, viewB);
IHypergraphQuery onlyA    = Difference(viewA, viewB);
```

**Union conflict rule: `a` wins** if a Handle resolves differently in both. **Only meaningful when
both sources share a Handle-identity space** — two views from the same kernel qualify; two
independently-constructed kernels do not (each allocator starts at Index 0).
`[verified:src=src/Topos.Hypergraph/UnionView.cs:8-23]`

### 4c — Version-diff via monotonic Handle.Index (in-kernel, no persistence)

A useful trick: because `Handle.Index` is monotonic and never reused, a Handle-Index threshold is a
genuine temporal cut of one kernel's history. `[verified:src=src/Topos.Hypergraph/HypergraphViews.cs:24-36]`

```csharp
uint snapshotAt = 1000;  // every Handle with Index < 1000 existed at "the snapshot point"

IHypergraphQuery asOfSnapshot = Subgraph(kernel, h => h.Index < snapshotAt);
IHypergraphQuery addedSince   = Difference(kernel, asOfSnapshot);  // "what's new since the snapshot"
```

This covers within-one-kernel-lifetime version-diffing today, with no persistence layer required.
Cross-process/cross-session snapshots are M4's job (`HypergraphSnapshot`).

---

## Pattern 5 — Semantic recall (vector search)

**When to use:** to find the k nearest vertices by embedding similarity — the retrieval half of a
RAG-style or memory-recall loop.

`VectorIndex` is a derived structure over `PropertyKey<float[]>` data; it adds no kernel storage and
swaps in a real ANN algorithm later without touching the kernel. Brute-force today (correct, simple);
true ANN is gated on a real workload's scale needs. `[verified:src=src/Topos.Hypergraph/VectorIndex.cs:3-19]`

```csharp
PropertyKey<float[]> embedding = kernel.ResolveProperty<float[]>("embedding");
var vectorIndex = new VectorIndex(kernel, embedding);

// Store embeddings on vertices as you create them.
kernel.SetProperty(embedding, turn1, [0.1f, 0.2f, 0.3f]);
kernel.SetProperty(embedding, turn2, [0.15f, 0.22f, 0.31f]);

// Recall the 5 nearest turns to a query embedding.
IReadOnlyList<(Handle Handle, float Distance)> nearest =
    vectorIndex.NearestNeighbors([0.12f, 0.21f, 0.32f], k: 5);
```

Squared Euclidean distance, ascending. Throws on `k <= 0` or embedding-dimension mismatch (no
padding/truncation). `[verified:src=src/Topos.Hypergraph/VectorIndex.cs:22-41]`

**Combined with feedback** — recall quality improves when you record whether each recall was useful.
Pattern 6's `EdgeStatistics` + a recall hyperedge closes that loop; see
`samples/Topos.Samples.ChatMemory.RecordRecallFeedback` for the worked example.
`[verified:src=samples/Topos.Samples.ChatMemory/ChatMemory.cs:104-113]`

---

## Pattern 6 — Learnable edges

**When to use:** when an edge's firing probability should learn from reward — the learning half of an
adaptive loop. Generalizes RLB's `HyperEdge` theta parameters without RLB's fixed feature layout.

```csharp
PropertyKey<LearnableEdge> edgeWeight = kernel.ResolveProperty<LearnableEdge>("edgeWeight");

// Initialize a learnable edge over 3 features (4 theta slots: 1 bias + 3 features).
Handle edge = kernel.CreateVertex(VertexRoles.Edge);
kernel.SetProperty(edgeWeight, edge, LearnableEdge.CreateUninitialized(featureCount: 3));

// Evaluate firing probability given a feature vector.
var features = new ReadOnlySpan<float>([1.0f, 0.5f, 0.2f]);
var current = kernel.TryGetProperty(edgeWeight, edge, out var w) ? w : LearnableEdge.CreateUninitialized(3);
float p = current.Evaluate(features);   // sigmoid(theta · [1, features...])

// Reinforce: take a gradient-ascent step toward reward, write the new edge back.
var reinforced = current.Reinforce(features, reward: 1.0f, learningRate: 0.1f);
kernel.SetProperty(edgeWeight, edge, reinforced);
```

`LearnableEdge` is an immutable value type — `Reinforce` returns a *new* instance and you `SetProperty`
it back over the old one, exactly like updating any other property. `[verified:src=src/Topos.Hypergraph/LearnableEdge.cs:3-14]`

**Per-membership statistics** ride alongside on the same edge:

```csharp
PropertyKey<EdgeStatistics> stats = kernel.ResolveProperty<EdgeStatistics>("stats");
var s = kernel.TryGetProperty(stats, edge, out var existing) ? existing : EdgeStatistics.Initial;
s = s.Observe(succeeded: true);   // EMA update: count++, SuccessRate and Confidence move toward outcome
kernel.SetProperty(stats, edge, s);
```

The EMA `Observe` rule is a default, not a mandate — a consumer with a different confidence model
computes their own `EdgeStatistics` and `SetProperty`s it. `[verified:src=src/Topos.Hypergraph/EdgeStatistics.cs:3-13]`

---

## Pattern 7 — Persistence (save / reload)

**When to use:** to round-trip a kernel through disk — checkpoint state, reload across sessions, or
hand a graph to another process.

`HypergraphSnapshot.Save/Load` serializes topology (vertices, incidences, allocator state) **plus an
explicit, caller-specified set of typed property columns**. It does not introspect property types
automatically — you name which properties to persist and how. `[verified:src=src/Topos.Hypergraph.Persistence/HypergraphSnapshot.cs:45-88]`

```csharp
using Topos.Hypergraph.Persistence;

PropertyKey<string>  name      = kernel.ResolveProperty<string>("name");
PropertyKey<float[]> embedding = kernel.ResolveProperty<float[]>("embedding");

var columns = new IPersistedPropertyColumn[]
{
    PersistedProperty.String(name),
    PersistedProperty.Single(embedding),   // float, not double
};

using (var stream = File.Create("memory.snap"))
    HypergraphSnapshot.Save(kernel, stream, columns);

HypergraphKernel reloaded;
using (var stream = File.OpenRead("memory.snap"))
    reloaded = HypergraphSnapshot.Load(stream, columns);   // same column list — names must match
```

**What this is and isn't:** `[verified:src=src/Topos.Hypergraph.Persistence/HypergraphSnapshot.cs:25-44]`

- It *is* an explicit save/load step that preserves Handle identity across reload (Invariant 1
  intact: `Save` writes `NextHandleIndex`, `Load` resumes allocation after it).
- It is *not* a transparent hot/cold hybrid kernel (no auto-spill under memory pressure).
- It is *not* an LSM tree (no WAL, no compaction, no crash safety beyond a completed write).
- Built-in codecs cover `int`/`long`/`double`/`float`/`bool`/`string`/`byte`/`DateOnly`. Anything else
  needs `PersistedProperty.Custom<T>` with a caller-supplied encode/decode pair.
  `[verified:src=src/Topos.Hypergraph.Persistence/PersistedProperty.cs:53-80]`

The transparent tiered version is real follow-on work, gated on a forcing workload. `LruCache<TKey,TValue>`
is the tested hot-tier building block that work would start from. `[verified:src=src/Topos.Hypergraph.Persistence/LruCache.cs:3-12]`

---

## Pattern 8 — Directed (role-aware) traversal

**When to use:** when your hyperedge has direction (Anchor→Target, Speaker→Mention, Before→After) and
the kernel's role-blind traversal would give the wrong answer. Requires the
`Topos.Hypergraph.Knowledge` package (M9).

Recall: every algorithm on `IHypergraphQuery` (`GetBfs`, `GetShortestPath`, etc.) is **role-blind**
by design — "the kernel does not judge." Two unrelated consumers (ChatMemory, NexusVerifier) plus
RLB's own `ToposGraphProjection` each hand-rolled the same ~10-line role-filtered walk before M9.
`DirectedBfs`/`DirectedShortestPath`/`RoleFilteredMembers` generalize that pattern into a tested
package. `[verified:src=src/Topos.Hypergraph.Knowledge/DirectedTraversal.cs:3-16]`

```csharp
using Topos.Hypergraph;
using Topos.Hypergraph.Knowledge;

public enum ChainerRole : byte { Anchor = 0, Condition = 1, Target = 2 }

IHypergraphQuery query = kernel;   // works over a kernel, a FilteredView, a UnionView — any source

// Multi-hop: from `a`, reach everyone reachable via Anchor→Target legs.
IReadOnlyList<Handle> reachable = query.DirectedBfs(a, ChainerRole.Anchor, ChainerRole.Target);

// One-hop: every Target of `a`'s hyperedges (generalizes ChatMemory.EntitiesMentionedIn).
IReadOnlyList<Handle> targets = query.RoleFilteredMembers(a, ChainerRole.Target);

// Shortest directed path from `a` to `c`.
IReadOnlyList<Handle> path = query.DirectedShortestPath(a, c, ChainerRole.Anchor, ChainerRole.Target);

// Adding incidences with the typed role, no manual cast.
kernel.AddIncidence(edge, target, ChainerRole.Target, ordinal: 2);
```

`DirectedBfs` follows only hyperedges where the frontier vertex holds `fromRole`, landing on that
edge's `toRole` members — so `Condition` members are correctly excluded from an Anchor→Target walk
even though they share the same hyperedges. `[verified:src=src/Topos.Hypergraph.Knowledge/DirectedTraversal.cs:19-51]`
`[verified:src=tests/Topos.Hypergraph.Knowledge.Tests/DirectedTraversalTests.cs:38-59]`

`TRole` must be a **byte-backed** enum (`enum Foo : byte`) — a wider underlying type throws
`ArgumentException`. `[verified:src=src/Topos.Hypergraph.Knowledge/RoleExtensions.cs:37-53]`

> **Don't reach for `HasCycle` for cycle detection in a directed graph.** At the kernel's role-blind
> layer, `HasCycle()` returns `true` almost always on any real n-ary hypergraph (three co-members are
> trivially "cyclic"). If you need directed cycle detection, walk with `DirectedBfs` and guard the
> current path yourself — which is what NexusVerifier's chainer does. `[verified:src=src/Topos.Hypergraph/IHypergraphQuery.cs:277-306]`

---

## Where to go next

- **Look up a type used above** → [`API_REFERENCE.md`](API_REFERENCE.md).
- **Read a real, working consumer end-to-end** → [`samples/Topos.Samples.ChatMemory/`](../samples/Topos.Samples.ChatMemory)
  — conversation turns, named entities, semantic recall, a feedback loop, all built from these patterns.
- **The role-byte convention** (why `enum : byte` and not `const byte`) → [`ROLE_CONVENTIONS.md`](ROLE_CONVENTIONS.md).
- **The thesis these patterns serve** → [`SPECIFICATION.md` §1–§7](SPECIFICATION.md).
