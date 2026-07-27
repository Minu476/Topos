# Getting started with Topos

**Date:** 2026-07-26 · **Author:** GLM-5.2 (ZCode) · **Audience:** a developer who has read
[`CONCEPTS.md`](CONCEPTS.md) (or at least its "four primitives" section) and wants to write their
first program. A linear, copy-pasteable walkthrough from "create a kernel" to "save and reload it."

> The examples here are adapted from the real, working code in
> [`samples/Topos.Samples.ChatMemory/`](../samples/Topos.Samples.ChatMemory) (the M5 falsifiability
> sample) and its [tests](../samples/Topos.Samples.ChatMemory.Tests/ChatMemoryTests.cs), stripped of
> the meta-framing. `[verified:src=samples/Topos.Samples.ChatMemory/ChatMemory.cs]`
> `[verified:src=samples/Topos.Samples.ChatMemory.Tests/ChatMemoryTests.cs]`

---

## 1. Add Topos to your project

Topos targets **.NET 10** and is **not yet on NuGet** — you reference it from source via a
`ProjectReference`. `[verified:src=src/Topos.Hypergraph/Topos.Hypergraph.csproj]` `[verified:docs=docs/DECISIONS.md — "M8 CLOSED" entry: NuGet-publish readiness is separately gated]`

```xml
<!-- in your consuming .csproj -->
<ItemGroup>
  <ProjectReference Include="path/to/Topos/src/Topos.Hypergraph/Topos.Hypergraph.csproj" />
</ItemGroup>
```

Three packages exist, each its own assembly: `[verified:src=src/Topos.Hypergraph/Topos.Hypergraph.csproj]`
`[verified:src=src/Topos.Hypergraph.Persistence/Topos.Hypergraph.Persistence.csproj]`
`[verified:src=src/Topos.Hypergraph.Knowledge/Topos.Hypergraph.Knowledge.csproj]`

| Package | What it's for | Required? |
|---|---|---|
| `Topos.Hypergraph` | The kernel + algorithms (storage contract, BFS/DFS, views, embeddings, learnable edges, analytics). | **Yes** — everything depends on this. |
| `Topos.Hypergraph.Persistence` | Save/load a kernel to/from disk (`HypergraphSnapshot`). | Only if you need persistence. |
| `Topos.Hypergraph.Knowledge` | Layer-1 role-aware directed traversal (`DirectedBfs`, `RoleFilteredMembers`, `AddIncidence<TRole>`). | Only if your domain has directed/role-gated semantics. |

To build and run the whole Topos solution (verify your environment): `[verified:src=Topos.sln]`

```bash
dotnet build Topos.sln
dotnet test Topos.sln
```

---

## 2. Create a kernel and some vertices

```csharp
using Topos.Hypergraph;

var kernel = new HypergraphKernel();

// Domain vertices — things in your domain. Allocate one, get its Handle back.
Handle alice   = kernel.CreateVertex();
Handle kyoto   = kernel.CreateVertex();
Handle nara    = kernel.CreateVertex();
Handle osaka   = kernel.CreateVertex();
```

`CreateVertex` allocates a fresh, never-reused `Handle` with `VertexStatus.Active`.
`[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:51-62]` A `Handle` is just an identity — it
carries no data. Data attaches via typed properties (step 3).

---

## 3. Define typed properties

Before you can attach data, you resolve a `PropertyKey<T>` for each typed attribute. **Resolve once,
cache the result** (e.g. in a field), and reuse it for every get/set — repeated resolution is a
dictionary lookup, not free, and the type `T` must be consistent for a given name.
`[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:144-152]`

```csharp
PropertyKey<string>  name      = kernel.ResolveProperty<string>("name");
PropertyKey<float[]> embedding = kernel.ResolveProperty<float[]>("embedding");

kernel.SetProperty(name, alice, "Alice");
kernel.SetProperty(name, kyoto, "Kyoto");

if (kernel.TryGetProperty(name, alice, out var aliceName))
    Console.WriteLine(aliceName);  // "Alice"
```

A few things worth knowing up front: `[verified:src=src/Topos.Hypergraph/PropertyKey.cs:6-26]`

- `PropertyKey<T>`'s constructor is `internal` — `ResolveProperty<T>` is the only way to get one
  (locked M8).
- No existence check on the `Handle` in `SetProperty` — properties on a not-yet-created or dormant
  Handle are legal (Invariant 1: provenance edges always resolve).
- Resolving the *same name* with two different `T`s is a caller error that throws
  `InvalidCastException` on first pool access. Pick one `T` per name and stick to it.

---

## 4. Build an n-ary hyperedge

This is the heart of Topos. One utterance mentioning three entities is **one atomic relationship**,
not three separate edges. Model it as one hyperedge — a vertex tagged `VertexRoles.Edge`, connected
to its members by incidences: `[verified:src=samples/Topos.Samples.ChatMemory/ChatMemory.cs:71-79]`

```csharp
// Define your domain's roles as a byte-backed enum (docs/ROLE_CONVENTIONS.md).
// [verified:docs=docs/ROLE_CONVENTIONS.md — "The decision"]
public enum TripRole : byte
{
    Speaker  = 0,  // the turn that said it
    Mention  = 1,  // an entity that was mentioned
}

// The hyperedge itself, reified as a vertex.
Handle mention = kernel.CreateVertex(VertexRoles.Edge);

// Wire up the members. Source is the edge; Member is each participant.
kernel.AddIncidence(mention, alice, (byte)TripRole.Speaker, ordinal: 0);
kernel.AddIncidence(mention, kyoto, (byte)TripRole.Mention, ordinal: 1);
kernel.AddIncidence(mention, nara,  (byte)TripRole.Mention, ordinal: 2);
kernel.AddIncidence(mention, osaka, (byte)TripRole.Mention, ordinal: 3);
```

`AddIncidence` takes the role as a raw `byte` — the kernel does not interpret it. Defining role
bytes as a `byte`-backed `enum` and casting explicitly is the documented convention; see
[`ROLE_CONVENTIONS.md`](ROLE_CONVENTIONS.md). `[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:116-126]`

---

## 5. Query it back

### One-hop: who was mentioned in this turn?

Hand-rolled (works against the kernel alone): `[verified:src=samples/Topos.Samples.ChatMemory/ChatMemory.cs:81-85]`

```csharp
IReadOnlyList<Handle> mentioned = kernel
    .GetVertexHyperedges(alice)              // hyperedges alice is in
    .SelectMany(edge => kernel.IncidencesFrom(edge))
    .Where(i => i.Role == (byte)TripRole.Mention)
    .Select(i => i.Member)
    .ToList();
// [kyoto, nara, osaka]
```

Equivalent, using the M9 `Knowledge` package (a one-liner once you reference it):
`[verified:src=src/Topos.Hypergraph.Knowledge/DirectedTraversal.cs:89-99]`

```csharp
using Topos.Hypergraph.Knowledge;

IReadOnlyList<Handle> mentioned = kernel.RoleFilteredMembers(alice, (byte)TripRole.Mention);
```

### Multi-hop topology: is `alice` connected to `osaka`?

Every kernel algorithm is a default method on `IHypergraphQuery`, built purely from the five
required primitives — `HypergraphKernel` *is* an `IHypergraphQuery`:
`[verified:src=src/Topos.Hypergraph/IHypergraphQuery.cs:57-159]`

```csharp
IHypergraphQuery query = kernel;

bool reachable = query.IsReachable(alice, osaka);             // true — they share the mention hyperedge
IReadOnlyList<Handle> path = query.GetShortestPath(alice, osaka);
foreach (var v in path)
    Console.WriteLine(v);   // #0, #3  (alice → osaka, one hop across the hyperedge)
```

A subtlety: at this layer, "adjacent" means *co-incident on the same hyperedge*. The `Speaker` /
`Mention` distinction is invisible to `IsReachable` — that's role-blind traversal by design
("the kernel does not judge"). The path is one hop because alice and osaka are both members of the
same hyperedge. `[verified:src=src/Topos.Hypergraph/IHypergraphQuery.cs:127-225]`

### Directed: follow only `Speaker → Mention` legs

If your domain has direction (a Speaker fires toward Mentions, an Anchor fires toward a Target),
use the M9 `Knowledge` package — the kernel's own traversal is deliberately role-blind:
`[verified:src=src/Topos.Hypergraph.Knowledge/DirectedTraversal.cs:19-51]`

```csharp
// From alice (Speaker), reach everyone she mentioned — multi-hop, across hyperedges.
IReadOnlyList<Handle> directed = query.DirectedBfs(alice, (byte)TripRole.Speaker, (byte)TripRole.Mention);
```

`DirectedBfs` follows only hyperedges where the current frontier vertex holds the `fromRole`, landing
on that edge's `toRole` members. It is an extension method on `IHypergraphQuery`, so it works over a
kernel, a filtered view, a union — any source.

---

## 6. Save and reload

To round-trip the kernel through disk, reference `Topos.Hypergraph.Persistence` and use
`HypergraphSnapshot`. You save topology (vertices, incidences, allocator state) **plus an explicit,
caller-specified set of typed property columns** — the snapshot machinery does not introspect
arbitrary property types automatically. `[verified:src=src/Topos.Hypergraph.Persistence/HypergraphSnapshot.cs:45-88]`

```csharp
using Topos.Hypergraph.Persistence;
using System.IO;

var columns = new IPersistedPropertyColumn[]
{
    PersistedProperty.String(name),         // the PropertyKey<string> from step 3
    // PersistedProperty.Single(embedding)  // float, not double — uncomment to persist embeddings
};

using (var stream = File.Create("trip.snap"))
    HypergraphSnapshot.Save(kernel, stream, columns);

HypergraphKernel reloaded;
using (var stream = File.OpenRead("trip.snap"))
    reloaded = HypergraphSnapshot.Load(stream, columns);

// Handle identity is intact across reload — Invariant 1 preserved.
Console.WriteLine(reloaded.CountVertices() == kernel.CountVertices()); // true
```

A few non-obvious things worth knowing: `[verified:src=src/Topos.Hypergraph.Persistence/HypergraphSnapshot.cs:25-44]`

- `Save` writes the kernel's `NextHandleIndex`; `Load` constructs the new kernel with that as its
  allocator's starting point, so the first vertex created post-reload gets a genuinely fresh Index —
  never a collision with a restored one.
- Built-in codecs cover common types (`int`, `long`, `double`, `float`, `bool`, `string`, `byte`,
  `DateOnly`). Anything else needs `PersistedProperty.Custom<T>` with a caller-supplied
  encode/decode pair. `[verified:src=src/Topos.Hypergraph.Persistence/PersistedProperty.cs:53-80]`
- This is **not** a transparent hot/cold hybrid kernel (no automatic spill under memory pressure),
  and **not** an LSM tree (no WAL/compaction/crash safety). It's an explicit save/load step. The
  transparent tiered version is real follow-on work, gated on a forcing workload — see the snapshot
  class's own scope note.

---

## 7. Where to go next

- **Look up a type you used** → [`API_REFERENCE.md`](API_REFERENCE.md).
- **Model a richer shape** (reification, per-cell data, composable views, learnable edges) →
  [`USAGE_PATTERNS.md`](USAGE_PATTERNS.md).
- **Read a real, working consumer end-to-end** → [`samples/Topos.Samples.ChatMemory/`](../samples/Topos.Samples.ChatMemory)
  — a non-RLB domain (conversation turns, named entities, semantic recall) using only the public API.
- **Understand the "why" behind the contract** → [`CONCEPTS.md`](CONCEPTS.md) and
  [`SPECIFICATION.md` §3](SPECIFICATION.md).
