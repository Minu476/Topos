# API reference

**Date:** 2026-07-26 · **Author:** GLM-5.2 (ZCode) · **Audience:** anyone looking up a specific type
or method. This is a hand-written prose catalog of every **public** type, grouped by layer/package.
For the mental model, see [`CONCEPTS.md`](CONCEPTS.md); for worked patterns,
[`USAGE_PATTERNS.md`](USAGE_PATTERNS.md).

> Conventions in this doc: each type has a one-sentence purpose, the load-bearing public members
> with signatures, a when-to-use note, and a source cross-ref. Every claim is tagged
> `[verified:src=...]`. Internal types (not public API) are listed in their own section at the end —
> they exist behind `InternalsVisibleTo` for tests/benchmarks, not for consumers.
>
> All types target **.NET 10** (`net10.0`) with `Nullable` and `ImplicitUsings` enabled.
> `[verified:src=src/Topos.Hypergraph/Topos.Hypergraph.csproj]`

---

## Kernel — `Topos.Hypergraph`

The four-primitive storage contract, the kernel class, and the query interface. Spec §3.
`[verified:docs=docs/SPECIFICATION.md §3]`

### `Handle`

Stable identity for a vertex. `[verified:src=src/Topos.Hypergraph/Handle.cs:17]`

```csharp
public readonly record struct Handle(uint Index, uint Generation = 0)
{
    public static readonly Handle Invalid = new(uint.MaxValue, uint.MaxValue);
    public bool IsValid => this != Invalid;
    public override string ToString();
}
```

- `Index` — monotonic, never-reused counter. A Handle's logical identity never changes, even after
  the vertex goes dormant (Invariant 1). `[verified:src=src/Topos.Hypergraph/Handle.cs:6-11]`
- `Generation` — reserved for a possible future slot-compaction feature. Always `0` today; load-bearing
  for nothing. `[verified:src=src/Topos.Hypergraph/Handle.cs:11-15]`
- `Invalid` — the "no vertex" sentinel. Failure paths on `TryGetVertex` set the out `Vertex`'s Handle
  to this value (not C#'s `default(Handle)`, which would collide with real vertex #0). Always check
  the `bool`; `IsValid` is defense-in-depth. `[verified:src=src/Topos.Hypergraph/Handle.cs:19-36]`

### `Vertex`

The node record. `[verified:src=src/Topos.Hypergraph/Vertex.cs:9]`

```csharp
public readonly record struct Vertex(Handle Handle, VertexRoles Roles, VertexStatus Status)
{
    public bool IsDormant => Status == VertexStatus.Dormant;
}
```

`Roles` and `Status` are inline struct fields (read per-hop by traversal), not PropertyBag entries —
the record is intentionally small. Typed data attaches separately via `PropertyKey<T>`.
`[verified:src=src/Topos.Hypergraph/Vertex.cs:3-8]`

### `Incidence`

One membership in a hyperedge. `[verified:src=src/Topos.Hypergraph/Incidence.cs:30]`

```csharp
public readonly record struct Incidence(Handle Source, Handle Member, byte Role, int Ordinal);
```

`Source` is the edge-vertex's Handle; `Member` is a participant. `Role` is a raw byte — the kernel
does not interpret it (define yours as a `byte`-backed `enum`, see [`ROLE_CONVENTIONS.md`](ROLE_CONVENTIONS.md)).
`Ordinal` is the position. **No cell-level properties exist on `Incidence` itself** — see
[`USAGE_PATTERNS.md`](USAGE_PATTERNS.md) for the two workarounds if you need per-membership data.
`[verified:src=src/Topos.Hypergraph/Incidence.cs:6-28]`

### `PropertyKey<T>`

Typed identity for a property. `[verified:src=src/Topos.Hypergraph/PropertyKey.cs:16-26]`

```csharp
public readonly record struct PropertyKey<T>
{
    internal PropertyKey(string name, int id);   // internal — locked M8
    public string Name { get; }
    public int Id { get; }
}
```

Obtain one only via `HypergraphKernel.ResolveProperty<T>(string)` (or `PropertyRegistry.Resolve<T>`)
— the constructor is `internal` to prevent constructing two keys with the same `Id` but different
`T`s (a footgun that throws `InvalidCastException` on first pool access).
`[verified:src=src/Topos.Hypergraph/PropertyKey.cs:6-15]`

### `PropertyRegistry`

Per-process, thread-safe string→int property registry. `[verified:src=src/Topos.Hypergraph/PropertyRegistry.cs:18-28]`

```csharp
public sealed class PropertyRegistry
{
    public PropertyKey<T> Resolve<T>(string name);
}
```

An Id is assigned once per name and never reused. The name→Id mapping is **untyped** — resolving the
same name with two different `T`s is a caller error that throws `InvalidCastException` on first pool
access. Single-writer discipline is the consumer's responsibility.
`[verified:src=src/Topos.Hypergraph/PropertyRegistry.cs:11-16]` In practice, most code calls
`kernel.ResolveProperty<T>` rather than instantiating a `PropertyRegistry` directly.

### `VertexRoles`

Kernel-level roles on a `Vertex` (read per-hop by traversal). `[verified:src=src/Topos.Hypergraph/VertexRoles.cs:13-17]`

```csharp
[Flags]
public enum VertexRoles : byte
{
    None = 0,
    Edge = 1 << 0,   // the one kernel role: "this vertex is a reified hyperedge"
}
```

Kernel-level only. Domain roles (Anchor/Condition/Target, Speaker/Mention, your domain's) do **not**
go here — they live as `Incidence.Role` bytes. `[verified:src=src/Topos.Hypergraph/VertexRoles.cs:3-11]`

### `VertexStatus`

Active/dormant tombstone flag. `[verified:src=src/Topos.Hypergraph/VertexStatus.cs:8-12]`

```csharp
public enum VertexStatus : byte
{
    Active = 0,
    Dormant = 1,
}
```

`Dormant` is a tombstone, not deletion — Invariant 1. Read per-hop to skip dormant vertices.
`[verified:src=src/Topos.Hypergraph/VertexStatus.cs:3-7]`

### `HandleAllocator`

Lock-free monotonic Handle allocator. `[verified:src=src/Topos.Hypergraph/HandleAllocator.cs:8-24]`

```csharp
public sealed class HandleAllocator
{
    public HandleAllocator(uint startingIndex = 0);
    public Handle Next();
    public uint NextIndex { get; }
}
```

Safe for concurrent callers (`Interlocked.Increment`). Never reuses an Index — Invariant 1 depends
on this. `startingIndex` exists for snapshot reload (M4) so allocation resumes after every loaded
Index. `[verified:src=src/Topos.Hypergraph/HandleAllocator.cs:4-22]` Most code never touches this
class directly — `HypergraphKernel` owns its own instance.

### `HypergraphKernel`

The kernel: storage + writes. Implements `IHypergraphQuery`. `[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:32]`

```csharp
public sealed class HypergraphKernel : IHypergraphQuery
{
    public HypergraphKernel();
    public HypergraphKernel(uint startingHandleIndex);   // M4: resume after reload
    public uint NextHandleIndex { get; }                 // M4: snapshot-side counterpart

    // ── Vertices ──
    public Handle CreateVertex(VertexRoles roles = VertexRoles.None);
    public bool TryGetVertex(Handle handle, out Vertex vertex);
    public void RestoreVertex(Handle handle, VertexRoles roles, VertexStatus status); // M4, snapshot only
    public void SetDormant(Handle handle);
    public void Reactivate(Handle handle);

    // ── Incidences ──
    public Incidence AddIncidence(Handle source, Handle member, byte role, int ordinal);
    public ImmutableArray<Incidence> IncidencesFrom(Handle source);
    public ImmutableArray<Incidence> IncidencesOf(Handle member);
    public IEnumerable<Incidence> AllIncidences();   // added for M4 snapshot consumer

    // ── Properties ──
    public PropertyKey<T> ResolveProperty<T>(string name);
    public void SetProperty<T>(PropertyKey<T> key, Handle handle, T value);
    public bool TryGetProperty<T>(PropertyKey<T> key, Handle handle, out T value);
    public bool RemoveProperty<T>(PropertyKey<T> key, Handle handle);
    public ImmutableArray<(Handle Handle, T Value)> EnumerateProperty<T>(PropertyKey<T> key);

    // ── IHypergraphQuery primitives (see below) ──
    public int CountVertices();
    public IReadOnlyList<Handle> VertexHandles();
    public IReadOnlyList<Handle> GetVertexHyperedges(Handle vertex);
    public IReadOnlyList<Incidence> GetHyperedgeVertices(Handle hyperedge);
}
```

Key operational facts: `[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:43-172]`

- **Concurrency:** Single-Writer/Multi-Reader. Handle allocation is lock-free; everything else is a
  sparse-set behind its own `ReaderWriterLockSlim`. Reads are always safe to call concurrently with
  the single writer; write methods assume a single-writer thread. `[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:6-31]`
- **No existence checks** on `AddIncidence` / `SetProperty` — provenance edges always resolve, even
  to dormant targets (Invariant 1). `[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:116-126]` `[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:154-156]`
- `RestoreVertex` is the **only sanctioned Invariant-1 bypass** — inserts a vertex at an
  already-allocated Handle, bypassing the allocator. Snapshot reload only; its only caller is
  `HypergraphSnapshot.Load`. `[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:71-81]`

### `IHypergraphQuery`

The query surface: 5 required primitives + ~40 default-implemented algorithms. Spec §6 M1.
`[verified:src=src/Topos.Hypergraph/IHypergraphQuery.cs:57]`

```csharp
public interface IHypergraphQuery
{
    // ── Required primitives ──
    int CountVertices();
    IReadOnlyList<Handle> VertexHandles();
    bool TryGetVertex(Handle handle, out Vertex vertex);
    IReadOnlyList<Handle> GetVertexHyperedges(Handle vertex);
    IReadOnlyList<Incidence> GetHyperedgeVertices(Handle hyperedge);

    // ── Default-implemented derivations ──
    bool IsEmpty();
    bool ContainsVertex(Handle handle);
    Vertex GetVertex(Handle handle);                       // throwing counterpart of TryGetVertex
    IReadOnlyList<Handle> HyperedgeHandles();               // O(V) scan for VertexRoles.Edge
    int CountHyperedges();

    // ── Default-implemented algorithms ──
    IEnumerable<Handle> GetBfs(Handle start);
    IEnumerable<Handle> GetDfs(Handle start);
    bool IsReachable(Handle from, Handle to);
    int? GetShortestPathLength(Handle from, Handle to);     // null if unreachable
    IReadOnlyList<Handle> GetShortestPath(Handle from, Handle to);
    bool HasCycle();
    IReadOnlyDictionary<Handle, IReadOnlyList<Handle>> GetTransitiveClosure();
    IReadOnlyList<IReadOnlyList<Handle>> GetConnectedComponents();
}
```

**Every algorithm here is role-blind and co-membership-symmetric** ("the kernel does not judge"). A
hop is one vertex→vertex step across one hyperedge; role direction is invisible. For role-aware /
directed traversal, use `Topos.Hypergraph.Knowledge` (below). `[verified:src=src/Topos.Hypergraph/IHypergraphQuery.cs:44-56]`

Two warnings worth reading in the source rather than paraphrased:

- **`HasCycle()` returns `true` almost always on any real n-ary hypergraph.** Three co-members are
  pairwise "adjacent," so any hyperedge with 3+ members is trivially cyclic at this layer. The
  useful question ("is there a cyclic *dependency* among Anchor→Target legs") needs role-aware
  traversal. `[verified:src=src/Topos.Hypergraph/IHypergraphQuery.cs:277-306]`
- **`GetConnectedComponents()` is deliberately not called "SCC."** At the topology layer the
  adjacency is symmetric, so strongly/weakly connected coincide. GDS-verified against `gds.wcc`.
  `[verified:src=src/Topos.Hypergraph/IHypergraphQuery.cs:358-410]`

GDS-parity coverage: BFS, DFS, and `GetConnectedComponents` are verified against `gds.bfs`/`gds.dfs`/
`gds.wcc` in `tests/Topos.Tests.GdsOracle`. `GetShortestPathLength` is verified with a documented
bipartite-vs-logical hop correction. Cycle detection and transitive closure are unit-tested only
(not standard GDS procedures). `[verified:src=src/Topos.Hypergraph/IHypergraphQuery.cs:36-42]`

---

## Reification & semantics — `Topos.Hypergraph`

Two public types that model epistemic status and provenance. **Neither is a reserved `Vertex` field** —
both are stored via `PropertyKey<T>` pools like any typed attribute, which is itself a small
validation that M0's property design was already general enough for M2/M5.

> There is **no dedicated reification method** on the kernel. Reification is a usage pattern:
> `CreateVertex(VertexRoles.Edge)` + `AddIncidence`. Nested reification (an edge that participates in
> another edge) is just an `Incidence` whose `Member` is an edge-vertex. `[verified:docs=docs/SPECIFICATION.md §7 pattern 12]`

### `AssertionMode`

Epistemic status of a reified relationship. `[verified:src=src/Topos.Hypergraph/AssertionMode.cs:20-30]`

```csharp
public enum AssertionMode : byte
{
    Asserted = 0,      // committed as true
    Quoted = 1,        // referenced without commitment (RDF 1.2 unasserted triple term)
    Hypothesized = 2,  // candidate belief, not yet confirmed/rejected
}
```

Not a reserved field — nothing in core traversal reads it per-hop. Resolve a property key for it:
`kernel.ResolveProperty<AssertionMode>("mode")`. A vertex with no mode property set means "no mode
recorded" — the kernel does not default a missing value to `Asserted`. `[verified:src=src/Topos.Hypergraph/AssertionMode.cs:3-19]`

### `Provenance`

Where a fact came from. `[verified:src=src/Topos.Hypergraph/Provenance.cs:17]`

```csharp
public readonly record struct Provenance(string Source, DateTimeOffset RecordedAt);
```

"First-class" here means a named designed type with clear semantics, not a new storage mechanism.
**For structural provenance** — which other in-graph facts a fact was derived from — nested
reification is the actual mechanism: link a derived edge to its source edges via `Incidence`. This
record is for the **leaf** case: provenance that terminates outside the graph (a document, a user,
an external system). `[verified:src=src/Topos.Hypergraph/Provenance.cs:4-16]`

---

## Views & set algebra — `Topos.Hypergraph`

Composable read-only views over `IHypergraphQuery`. Spec §6 M3.

### `HypergraphViews`

Static factory for composable views. All methods return `IHypergraphQuery` and are O(1) to construct.
`[verified:src=src/Topos.Hypergraph/HypergraphViews.cs:37-57]`

```csharp
public static class HypergraphViews
{
    public static IHypergraphQuery Subgraph(IHypergraphQuery source, Func<Handle, bool> predicate);
    public static IHypergraphQuery Mask(IHypergraphQuery source, Func<Handle, bool> predicate);     // same as Subgraph
    public static IHypergraphQuery Union(IHypergraphQuery a, IHypergraphQuery b);
    public static IHypergraphQuery Intersect(IHypergraphQuery a, IHypergraphQuery b);
    public static IHypergraphQuery Difference(IHypergraphQuery a, IHypergraphQuery b);
}
```

Notes: `[verified:src=src/Topos.Hypergraph/HypergraphViews.cs:3-36]`

- **No `Unmodifiable` view exists, deliberately.** `IHypergraphQuery` has no write members, so typing
  a kernel as `IHypergraphQuery` already gives an unmodifiable view at compile time, for free.
- **Set algebra doubles as in-kernel version-diff.** Because `Handle.Index` is monotonic and never
  reused, `Subgraph(kernel, h => h.Index < threshold)` is a genuine "state as of an earlier point in
  this kernel's history." `Difference(later, earlier)` gives "what's been added since," with no
  persistence layer required. See [`USAGE_PATTERNS.md`](USAGE_PATTERNS.md).

### `FilteredView`

Read-only view restricting a source to vertices passing a predicate. `[verified:src=src/Topos.Hypergraph/FilteredView.cs:24-42]`

```csharp
public sealed class FilteredView(IHypergraphQuery source, Func<Handle, bool> predicate) : IHypergraphQuery;
```

`Subgraph` and `Mask` both construct one of these. A hyperedge is included only when the predicate
accepts the edge-vertex itself; a member is reported only when the predicate also accepts that member
(JGraphT `AsSubgraph` convention generalized to N-ary — out-of-view members are silently dropped,
not errors). Every `IHypergraphQuery` algorithm works over a `FilteredView` unchanged.
`[verified:src=src/Topos.Hypergraph/FilteredView.cs:11-23]`

### `UnionView`

Read-only view presenting the union of two sources. `[verified:src=src/Topos.Hypergraph/UnionView.cs:24-42]`

```csharp
public sealed class UnionView(IHypergraphQuery a, IHypergraphQuery b) : IHypergraphQuery;
```

**Conflict rule: `a` wins** if the same Handle resolves differently in both. **Only meaningful when
both sources share a Handle-identity space** — two views from the same kernel qualify; two
independently-constructed kernels do not (each allocator starts at Index 0, so `Handle(3)` in each
is almost certainly unrelated). `[verified:src=src/Topos.Hypergraph/UnionView.cs:8-23]`

---

## Embeddings & learnable edges — `Topos.Hypergraph`

Derived structures over `PropertyKey<T>` data. **None of these adds kernel storage** — they read
from the kernel's existing typed properties via `EnumerateProperty`, so each is swappable without
touching `HypergraphKernel`. Spec §6 M5.

### `VectorIndex`

k-nearest-neighbor search over `PropertyKey<float[]>` embeddings. `[verified:src=src/Topos.Hypergraph/VectorIndex.cs:20-56]`

```csharp
public sealed class VectorIndex(HypergraphKernel kernel, PropertyKey<float[]> embeddingKey)
{
    public IReadOnlyList<(Handle Handle, float Distance)> NearestNeighbors(ReadOnlySpan<float> query, int k);
}
```

**Brute-force, not approximate** — the name says "VectorIndex," not "ApproximateNearestNeighborIndex,"
to avoid overclaiming. A true ANN (HNSW/IVF/LSH) is real follow-on work, gated on a real workload's
scale needs. Squared Euclidean distance; throws on `k <= 0` or embedding-dimension mismatch (no
padding/truncation). `[verified:src=src/Topos.Hypergraph/VectorIndex.cs:3-19]`

### `LearnableEdge`

Sigmoid edge weight, reinforced by gradient ascent on reward. `[verified:src=src/Topos.Hypergraph/LearnableEdge.cs:15-56]`

```csharp
public readonly record struct LearnableEdge(float[] Theta)
{
    public float Evaluate(ReadOnlySpan<float> features);                 // theta[0] is bias
    public LearnableEdge Reinforce(ReadOnlySpan<float> features, float reward, float learningRate);
    public static LearnableEdge CreateUninitialized(int featureCount);   // all-zero → Evaluate returns 0.5
}
```

Generalizes RLB's `ThetaParameters`/`ReinforceTheta` without RLB's fixed feature layout — the
feature vector length is whatever the caller supplies. Immutable value type: `Reinforce` returns a
*new* instance; the caller `SetProperty`s it back over the old one, exactly like updating any other
property. `[verified:src=src/Topos.Hypergraph/LearnableEdge.cs:3-14]`

### `EdgeStatistics`

Per-membership statistics carried on an edge. `[verified:src=src/Topos.Hypergraph/EdgeStatistics.cs:14-29]`

```csharp
public readonly record struct EdgeStatistics(int TransitionCount, double SuccessRate, double Confidence)
{
    public static readonly EdgeStatistics Initial = new(0, 1.0, 0.5);
    public EdgeStatistics Observe(bool succeeded, double smoothing = 0.1);  // EMA update
}
```

Generalizes RLB's `TransitionCount`/`SuccessRate`/`Confidence`. The EMA `Observe` rule is a sensible
default, not a mandated one — a consumer with a different confidence model can compute their own and
`SetProperty` it instead. `[verified:src=src/Topos.Hypergraph/EdgeStatistics.cs:3-13]`

---

## Analytics — `Topos.Hypergraph`

M6 algorithms over topology-only bipartite adjacency. Spec §6 M6.

### `SWalk`

s-walk / s-distance over hyperedges. The one genuinely hypergraph-specific algorithm here.
`[verified:src=src/Topos.Hypergraph/SWalk.cs:15-105]`

```csharp
public static class SWalk
{
    public static IEnumerable<Handle> Reachable(IHypergraphQuery graph, Handle start, int s);
    public static int? Distance(IHypergraphQuery graph, Handle from, Handle to, int s);
}
```

Two hyperedges are **s-adjacent** when they share ≥ `s` common members; an s-walk is a path of
s-adjacent hyperedges; s-distance is the shortest such path. **Deliberately not GDS-verified** — GDS
operates on the binary projection and has no notion of "share ≥ s members"; Topos's answer is the
novel claim here. Both methods throw `ArgumentOutOfRangeException` eagerly (not on enumeration) if
`s < 1`. `[verified:src=src/Topos.Hypergraph/SWalk.cs:3-14]` `[verified:src=src/Topos.Hypergraph/SWalk.cs:17-31]`

### `LabelPropagation`

Community detection by label propagation. GDS-verified (`gds.labelPropagation`).
`[verified:src=src/Topos.Hypergraph/LabelPropagation.cs:17-65]`

```csharp
public static class LabelPropagation
{
    public static IReadOnlyDictionary<Handle, int> DetectCommunities(IHypergraphQuery graph, int maxIterations = 100);
}
```

Returns a vertex→community-id map. Ties broken by lowest label; uses a fixed seed (`12345`) for
deterministic output; isolated vertices keep their own label. **Chosen over Louvain deliberately** —
Louvain's multi-level modularity optimization is substantially more complex to get right, and
Label Propagation gives real, GDS-verified community detection today. Louvain is real follow-on work.
`[verified:src=src/Topos.Hypergraph/LabelPropagation.cs:3-15]`

### `TriangleCount`

Triangle counting over bipartite adjacency. GDS-verified (`gds.triangleCount`).
`[verified:src=src/Topos.Hypergraph/TriangleCount.cs:24-50]`

```csharp
public static class TriangleCount
{
    public static long Count(IHypergraphQuery graph);
}
```

Ordered-neighbor-pairs method — counts each triangle once. **Non-obvious consequence of the bipartite
reification:** an N-member hyperedge and its members form a complete graph on N+1 vertices, giving
`C(N+1, 3)` triangles from one hyperedge. A plain 2-member edge already yields 1 triangle
(`C(3,3) = 1`); a 3-member edge yields 4. `[verified:src=src/Topos.Hypergraph/TriangleCount.cs:13-23]`

### `Modularity`

Newman's modularity Q for a given partition. **A scorer, not a detector** — pass it
`LabelPropagation.DetectCommunities`'s output. `[verified:src=src/Topos.Hypergraph/Modularity.cs:12-72]`

```csharp
public static class Modularity
{
    public static double Compute(IHypergraphQuery graph, IReadOnlyDictionary<Handle, int> communities);
}
```

Returns `0.0` for an edgeless graph. **Asymmetry to know:** vertices missing from `communities` are
excluded from the internal-edges numerator but grouped into a synthetic community (key `-1`) for the
degree-sum-of-squares penalty — so omitting vertices can only push Q down, never up. Pass a partition
covering every vertex for a meaningful score. `[verified:src=src/Topos.Hypergraph/Modularity.cs:15-28]`

---

## Persistence — `Topos.Hypergraph.Persistence`

Save/load a kernel's topology + caller-specified property columns to/from a `Stream`. Spec §6 M4.

### `HypergraphSnapshot`

Static save/load entry points. `[verified:src=src/Topos.Hypergraph.Persistence/HypergraphSnapshot.cs:45-139]`

```csharp
public static class HypergraphSnapshot
{
    public static void Save(HypergraphKernel kernel, Stream stream, IReadOnlyList<IPersistedPropertyColumn>? properties = null);
    public static HypergraphKernel Load(Stream stream, IReadOnlyList<IPersistedPropertyColumn>? properties = null);
}
```

Versioned binary format (magic `0x53485054` "TPHS", format version 1). Columnar for properties — the
on-disk shape mirrors the in-memory sparse-set dense arrays. **Invariant 1 preserved across reload:**
`Save` writes `NextHandleIndex`; `Load` constructs the new kernel with that as the allocator's start,
so the first post-reload vertex gets a fresh Index, never a collision.
`[verified:src=src/Topos.Hypergraph.Persistence/HypergraphSnapshot.cs:16-23]`

**Scope, plainly stated:** this is *not* a transparent hot/cold hybrid kernel (no auto-spill under
memory pressure) and *not* an LSM tree (no WAL/compaction/crash safety beyond a completed write).
The transparent tiered version is real follow-on work. `[verified:src=src/Topos.Hypergraph.Persistence/HypergraphSnapshot.cs:25-44]`

### `IPersistedPropertyColumn`

Non-generic handle for a heterogeneous list of typed property columns.
`[verified:src=src/Topos.Hypergraph.Persistence/PersistedProperty.cs:9-14]`

```csharp
public interface IPersistedPropertyColumn
{
    string Name { get; }
    void WriteTo(HypergraphKernel kernel, BinaryWriter writer);
    void ReadFrom(HypergraphKernel kernel, BinaryReader reader);
}
```

Lets `Save`/`Load` take one flat `IReadOnlyList` mixing columns of different `T`s. The generic
implementing class `PersistedPropertyColumn<T>` is `internal`.

### `PersistedProperty`

Factory for column codecs — common types plus a custom escape hatch. `[verified:src=src/Topos.Hypergraph.Persistence/PersistedProperty.cs:51-81]`

```csharp
public static class PersistedProperty
{
    public static IPersistedPropertyColumn Int32(PropertyKey<int> key);
    public static IPersistedPropertyColumn Int64(PropertyKey<long> key);
    public static IPersistedPropertyColumn Double(PropertyKey<double> key);
    public static IPersistedPropertyColumn Single(PropertyKey<float> key);
    public static IPersistedPropertyColumn Boolean(PropertyKey<bool> key);
    public static IPersistedPropertyColumn String(PropertyKey<string> key);
    public static IPersistedPropertyColumn Byte(PropertyKey<byte> key);
    public static IPersistedPropertyColumn DateOnly(PropertyKey<DateOnly> key);   // day-number encoded
    public static IPersistedPropertyColumn Custom<T>(PropertyKey<T> key, Action<BinaryWriter, T> write, Func<BinaryReader, T> read);
}
```

Deliberately not fully-generic-automatic — the caller states up front which properties to persist and
how. `[verified:src=src/Topos.Hypergraph.Persistence/PersistedProperty.cs:45-50]`

### `LruCache<TKey, TValue>`

Classic O(1) LRU cache — the tested hot-tier building block for future tiered storage.
`[verified:src=src/Topos.Hypergraph.Persistence/LruCache.cs:13]`

```csharp
public sealed class LruCache<TKey, TValue> where TKey : notnull
{
    public LruCache(int capacity);
    public int Capacity { get; }
    public int Count { get; }
    public bool TryGet(TKey key, out TValue value);
    public bool ContainsKey(TKey key);
    public (TKey Key, TValue Value)? Set(TKey key, TValue value);  // returns evicted pair, if any
    public bool Remove(TKey key);
}
```

Dictionary + intrusive doubly-linked list. Not thread-safe on its own. `Set` returns the evicted pair
if the insertion caused an eviction — the caller's cue to flush to the cold tier.
`[verified:src=src/Topos.Hypergraph.Persistence/LruCache.cs:3-12]`

---

## Knowledge — `Topos.Hypergraph.Knowledge`

Layer-1 role-aware directed traversal. Spec §6 M9. A separate package (own assembly); pure consumer
of `IHypergraphQuery` — **no kernel changes.** Generalizes a pattern three independent consumers
(ChatMemory, NexusVerifier, Rich-Learning-Base's `ToposGraphProjection`) each hand-rolled.
`[verified:src=src/Topos.Hypergraph.Knowledge/Topos.Hypergraph.Knowledge.csproj]`

### `DirectedTraversal`

Role-aware directed traversal over `IHypergraphQuery`. `[verified:src=src/Topos.Hypergraph.Knowledge/DirectedTraversal.cs:17-113]`

```csharp
public static class DirectedTraversal
{
    // BFS following only hyperedges where the frontier vertex holds fromRole, landing on toRole members.
    public static IReadOnlyList<Handle> DirectedBfs(this IHypergraphQuery graph, Handle start, byte fromRole, byte toRole);

    // One shortest directed path following only fromRole→toRole legs. Empty if unreachable; [from] if from==to.
    public static IReadOnlyList<Handle> DirectedShortestPath(this IHypergraphQuery graph, Handle from, Handle to, byte fromRole, byte toRole);

    // One-hop: members of vertex's hyperedges holding the given role.
    public static IReadOnlyList<Handle> RoleFilteredMembers(this IHypergraphQuery graph, Handle vertex, byte role);
}
```

This is where "the kernel does not judge" gets its judgment: given a directed reading of hyperedge
roles (e.g. an Anchor firing toward a Target), walk only along that direction. All three are
extension methods on `IHypergraphQuery`, so they work over a kernel, a `FilteredView`, a `UnionView` —
any source. `[verified:src=src/Topos.Hypergraph.Knowledge/DirectedTraversal.cs:3-16]`

### `RoleExtensions`

Turns [`ROLE_CONVENTIONS.md`](ROLE_CONVENTIONS.md)'s byte-backed-enum pattern into real code —
typed-role overloads of the above, plus `AddIncidence<TRole>`. `[verified:src=src/Topos.Hypergraph.Knowledge/RoleExtensions.cs:15-54]`

```csharp
public static class RoleExtensions
{
    public static Incidence AddIncidence<TRole>(this HypergraphKernel kernel, Handle source, Handle member, TRole role, int ordinal)
        where TRole : unmanaged, Enum;

    public static IReadOnlyList<Handle> DirectedBfs<TRole>(this IHypergraphQuery graph, Handle start, TRole fromRole, TRole toRole)
        where TRole : unmanaged, Enum;

    public static IReadOnlyList<Handle> DirectedShortestPath<TRole>(this IHypergraphQuery graph, Handle from, Handle to, TRole fromRole, TRole toRole)
        where TRole : unmanaged, Enum;

    public static IReadOnlyList<Handle> RoleFilteredMembers<TRole>(this IHypergraphQuery graph, Handle vertex, TRole role)
        where TRole : unmanaged, Enum;
}
```

`TRole` must be a **byte-backed** enum (`enum Foo : byte`) — a wider underlying type (e.g. `int`)
throws `ArgumentException` rather than silently truncating. `[verified:src=src/Topos.Hypergraph.Knowledge/RoleExtensions.cs:37-53]`

Usage: `[verified:src=tests/Topos.Hypergraph.Knowledge.Tests/DirectedTraversalTests.cs:38-46]`

```csharp
public enum ChainerRole : byte { Anchor = 0, Condition = 1, Target = 2 }

IHypergraphQuery query = kernel;
var reachable = query.DirectedBfs(start, ChainerRole.Anchor, ChainerRole.Target);
kernel.AddIncidence(edge, target, ChainerRole.Target, ordinal: 2);
```

---

## Internal types — not public API

These exist behind `InternalsVisibleTo` (granted to `Topos.Hypergraph.Tests` and
`Topos.Hypergraph.Benchmarks` in `Topos.Hypergraph.csproj`) for direct test/benchmark access. They
are **not** part of the public surface and may change without notice.

`[verified:src=src/Topos.Hypergraph/Topos.Hypergraph.csproj — InternalsVisibleTo entries]`

| Type | Source | What it is |
|---|---|---|
| `SparseSet<T>` | `src/Topos.Hypergraph/SparseSet.cs` | EnTT-style columnar pool backing every property/vertex store. O(1) add/remove/lookup; the kernel's public surface is `IHypergraphQuery`/`HypergraphKernel`, not this. `[verified:src=src/Topos.Hypergraph/SparseSet.cs:18-24]` |
| `PropertyPool<T>` | `src/Topos.Hypergraph/PropertyPool.cs` | A `SparseSet<T>` behind its own `ReaderWriterLockSlim` — the per-pool-lock of spec §3.4. |
| `IncidenceIndex` | `src/Topos.Hypergraph/IncidenceIndex.cs` | Key→many-Incidences index backing both kernel directions; replaced an O(N²) copy-on-write design. |
| `BipartiteAdjacency` | `src/Topos.Hypergraph/BipartiteAdjacency.cs` | Shared neighbor-gathering for M6 analytics. `[verified:src=src/Topos.Hypergraph/BipartiteAdjacency.cs:13]` |
| `PersistedPropertyColumn<T>` | `src/Topos.Hypergraph.Persistence/PersistedProperty.cs:16` | Generic implementing class of `IPersistedPropertyColumn`. `[verified:src=src/Topos.Hypergraph.Persistence/PersistedProperty.cs:16-43]` |

If you find yourself wanting one of these from a consumer, that's a signal worth reporting — either
your use case is genuinely novel (and the public surface should grow), or there's a public-API way
to do what you want that you haven't found. File it as a finding rather than reaching for
`InternalsVisibleTo`.

---

## Cross-references

- **The full spec** (storage contract, layer architecture, roadmap, design patterns) → [`SPECIFICATION.md`](SPECIFICATION.md).
- **What's locked vs. open** → [`SPECIFICATION.md` §11](SPECIFICATION.md) (quick-reference table) and
  [`DECISIONS.md`](DECISIONS.md) (the decision log).
- **Role-byte conventions** (the `byte`-backed-enum pattern) → [`ROLE_CONVENTIONS.md`](ROLE_CONVENTIONS.md).
- **GDS-oracle setup** (the Neo4j correctness oracle behind the GDS-verified claims above) →
  [`GDS_ORACLE_SETUP.md`](GDS_ORACLE_SETUP.md).
