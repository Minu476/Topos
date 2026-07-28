# M10 proposal — `Topos.Hypergraph.Mcp` server

**Date:** 2026-07-27 (authored overnight 2026-07-26) · **Author:** GLM-5.2 (ZCode) · **Status:**
🔒 **APPROVED AND IMPLEMENTED (2026-07-27).** Nasser reviewed this proposal, was shown two
corrections found by checking rather than trusting it (the SDK's license is Apache-2.0, not MIT as
§2.4 originally claimed; `~/Projects/FSDE`'s own MCP server deliberately hand-rolled its JSON-RPC
layer instead of using the official SDK), then gave an explicit go-ahead plus explicit answers to
all four §5 forks that gate v1 scope. A code-authoring session (Sonnet 5) executed §6 the same day.
See `docs/DECISIONS.md`'s "M10 APPROVED AND IMPLEMENTED" entry for the full build record — what
shipped, what deviated from this doc's sketch (a `sealed class`, not `static class`, for the tool
type; `resolve_property` dropped as its own tool; tool results are JSON-serialized to `string`
rather than assumed to auto-serialize), and what's still open (literal agent-in-the-loop dogfood,
pending a Claude Code restart to pick up the new `topos` entry in `.mcp.json`).

The rest of this document is preserved as originally authored — the proposal, its evidence, and
its (now-resolved) open forks — as the design record. Don't read the 🟡/PROPOSED framing below as
current status; the header above is authoritative.

> **Lane note:** This is a design proposal, not an applied change. Implementing it is C# code under
> a new `src/Topos.Hypergraph.Mcp/` package — outside the documentation-only role GLM-5.2 holds in
> this repo (`docs/GLM_DOCUMENTATION_GUIDELINES.md §3`). A code-authoring session (Claude/Sonnet,
> the code-authoring role per `docs/SESSION_HANDOFF.md`) executes this; the implementation steps in
> §6 are concrete enough to do so without further design.

---

## 1. The proposal in one paragraph

Build a new `Topos.Hypergraph.Mcp` package that exposes Topos's public API as **Model Context
Protocol (MCP) tools**, so any MCP-aware agent (Claude Code, Cursor, Continue, ZCode, anything
speaking the 2025-11-25 spec) can create vertices, build n-ary hyperedges, query traversals, and
run directed/role-aware search against a Topos kernel — without writing any C#. The MCP C# SDK is
now v1.0 and Microsoft-shipped (`[verified:web=...]` below), so this is a thin JSON-RPC wrapper
over primitives that already exist and are tested, not new graph logic. **The forcing-function case
is unusually strong:** Topos's stated reason to exist is AI agent memory, and MCP is the standard
protocol agents use to talk to tools — building the bridge is the most aligned next step, not a
speculative extension. `[verified:docs=docs/SPECIFICATION.md §2.1 — "purpose-fit for AI / agent memory"]`

---

## 2. Why M10 = MCP (the forcing-function case)

### 2.1 Topos's thesis is AI memory; MCP is the AI-tool protocol

Topos is "purpose-fit for AI / agent memory" — that's the opening line of the spec and the README.
`[verified:src=README.md]` `[verified:docs=docs/SPECIFICATION.md §2.1]` The systems actually
competing for the agent-memory niche (Zep/Graphiti, mem0, Letta, Cognee) all expose themselves to
agents through some integration surface; MCP is fast becoming the *standard* surface, with first
parties (Anthropic, Microsoft, OpenAI) shipping SDKs and clients.
`[verified:web=https://devblogs.microsoft.com/dotnet/release-v10-of-the-official-mcp-csharp-sdk/]`
A Topos that an agent can talk to directly is a Topos that fulfills its thesis; a Topos that
requires the agent's author to write C# bindings is a Topos that's harder to adopt than its binary
competitors.

### 2.2 The kernel primitives map almost 1:1 to MCP tool calls

Look at the existing public surface — every line is a tool an agent would plausibly call:

| Topos primitive (source-verified) | Natural MCP tool |
|---|---|
| `Handle CreateVertex(VertexRoles roles)` `[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:51-62]` | `create_vertex` |
| `Incidence AddIncidence(Handle source, Handle member, byte role, int ordinal)` `[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:116-126]` | `add_incidence` |
| `void SetProperty<T>(PropertyKey<T> key, Handle handle, T value)` `[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:154-156]` | `set_property` |
| `bool TryGetProperty<T>(...)` / `bool IsReachable(...)` / `IReadOnlyList<Handle> GetShortestPath(...)` `[verified:src=src/Topos.Hypergraph/IHypergraphQuery.cs:57-262]` | `get_property` / `is_reachable` / `shortest_path` |
| `DirectedBfs` / `RoleFilteredMembers` `[verified:src=src/Topos.Hypergraph.Knowledge/DirectedTraversal.cs:19-99]` | `directed_bfs` / `role_filtered_members` |
| `NearestNeighbors(query, k)` `[verified:src=src/Topos.Hypergraph/VectorIndex.cs:30-41]` | `semantic_recall` |

There is **no new graph logic to design.** The work is mapping types (`Handle` ↔ JSON, `byte` role ↔
int, generic `<T>` ↔ a tagged-union property value) and writing the JSON schemas. The kernel's
primitives already exist and are covered by the 179-test suite. `[verified:docs=docs/SESSION_HANDOFF.md §2]`

### 2.3 The "kernel records; it does not judge" philosophy maps cleanly

The M8/M9 discipline — kernel stores role bytes faithfully, doesn't validate cardinalities, layer 1
attaches semantics (`[verified:src=src/Topos.Hypergraph/Incidence.cs:6-15]`) — has a natural MCP
analogue: the MCP server exposes raw primitives as tools and **does not enforce a schema on the
agent**. The agent (layer 1) decides what role bytes mean, what cardinalities to enforce, how to
chain tool calls. The server stays dumb and faithful, exactly like the kernel. This isn't a stretch
mapping; it's the same principle in a new shape.

### 2.4 The infrastructure now exists — no roll-your-own JSON-RPC

As of early 2026, Microsoft ships a v1.0 MCP C# SDK that fully supports the 2025-11-25 spec:
`[verified:web=https://devblogs.microsoft.com/dotnet/release-v10-of-the-official-mcp-csharp-sdk/]`
`[verified:web=https://github.com/modelcontextprotocol/csharp-sdk]`

- **Package:** `ModelContextProtocol` (main, with hosting/DI) or `ModelContextProtocol.AspNetCore`
  (HTTP transport). `[verified:web=https://github.com/modelcontextprotocol/csharp-sdk — README]`
- **Tool definition shape** (verified directly from the SDK's own quickstart sample,
  `samples/QuickstartWeatherServer/Tools/WeatherTools.cs`): a class tagged
  `[McpServerToolType]`, methods tagged `[McpServerTool]` with `[Description("...")]` on the method
  and each parameter — the SDK introspects these into the MCP tool schema automatically.
  `[verified:web=https://github.com/modelcontextprotocol/csharp-sdk/blob/main/samples/QuickstartWeatherServer/Tools/WeatherTools.cs]`
- **Host setup** (from the same sample's `Program.cs`):
  `builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<ToposTools>()` — three
  lines. `[verified:web=https://github.com/modelcontextprotocol/csharp-sdk/blob/main/samples/QuickstartWeatherServer/Program.cs]`
- **Transports:** stdio (the canonical agent transport — what Claude Code/Cursor/etc. speak) plus
  HTTP/SSE via the AspNetCore package. `[verified:web=https://github.com/modelcontextprotocol/csharp-sdk]`
- **License:** ~~MIT~~ **Correction (2026-07-27): Apache-2.0**, confirmed via the NuGet package
  listing — this doc's original claim, sourced from the SDK docs site, was wrong. Still compatible
  as a dependency of Topos's MIT-licensed code (Apache-2.0 doesn't require the consumer to relicense),
  just not literally "MIT" as originally stated here.

A v1 server is plausibly ~300–500 lines of C# on top of this SDK, almost all of it mechanical
type-mapping. That's the same order of magnitude as M9 (`Topos.Hypergraph.Knowledge`, ~170 lines of
real logic `[verified:src=src/Topos.Hypergraph.Knowledge/DirectedTraversal.cs]` +
`[verified:src=src/Topos.Hypergraph.Knowledge/RoleExtensions.cs]`), and M9 was scoped and built in a
single session. `[verified:docs=docs/DECISIONS.md — "M9 IMPLEMENTED" entry]`

---

## 3. What it is — and what it isn't

### Is

- A new package, `Topos.Hypergraph.Mcp`, with its own assembly — matching the M4 (`.Persistence`)
  and M9 (`.Knowledge`) packaging-split precedent of splitting at a real architectural boundary.
  `[verified:src=src/Topos.Hypergraph.Persistence/Topos.Hypergraph.Persistence.csproj]`
  `[verified:src=src/Topos.Hypergraph.Knowledge/Topos.Hypergraph.Knowledge.csproj]`
- A thin JSON-RPC wrapper over Topos's existing public surface, built on the official Microsoft
  MCP C# SDK. No kernel changes; the kernel stays clean (dependency direction preserved: the MCP
  package references `Topos.Hypergraph`, never the reverse).
- A consumer of Topos, not a feature of it. The kernel does not learn that MCP exists; the same
  way RLB's `ToposGraphProjection` is a consumer, not a kernel feature.

### Is not

- **Not an extraction pipeline.** The server doesn't run LLMs, parse text into triples, or do
  entity recognition. It exposes storage/query primitives; the *agent* does any extraction and
  calls `create_vertex` / `add_incidence`. (Same scope boundary as the kernel itself — spec §2.2.)
  `[verified:docs=docs/SPECIFICATION.md §2.2]`
- **Not a reasoning engine.** No contradiction resolution, no entailment, no belief revision. The
  server stores `AssertionMode.Hypothesized` if the agent asks it to; it doesn't decide what
  hypothesized means. (Same scope boundary.)
- **Not a replacement for any existing package.** It's purely additive: a new way for agents to
  reach the same kernel a C# consumer reaches directly.

---

## 4. Proposed package structure

```
src/Topos.Hypergraph.Mcp/
├── Topos.Hypergraph.Mcp.csproj          # PackageReference: ModelContextProtocol (+ AspNetCore for HTTP)
│                                         # ProjectReference: Topos.Hypergraph (+ .Knowledge, + .Persistence for v2)
├── ToposMcpServer.cs                    # The McpServerToolType class — all the [McpServerTool] methods
├── TypeMapping.cs                       # Handle ↔ JSON, byte role ↔ int, PropertyKey<T> ↔ tagged-union value
├── Transport/                           # v1: stdio only. v2: HTTP/SSE option.
│   └── StdioHost.cs                     # The 3-line host: AddMcpServer().WithStdioServerTransport().WithTools<>()
└── (v2 only) StatefulSession.cs         # Session-keyed kernel lifecycle — see §5 fork (b)
```

Mirrors the existing `src/Topos.Hypergraph.{Persistence,Knowledge}/` layout exactly — one package,
one assembly, ProjectReferences back to the kernel. `[verified:src=src/Topos.Hypergraph.Persistence/Topos.Hypergraph.Persistence.csproj]`
`[verified:src=src/Topos.Hypergraph.Knowledge/Topos.Hypergraph.Knowledge.csproj]`

### Tool surface (v1 sketch — final names are §5 fork (e))

Stateless v1 would expose roughly these tools (each ~5–10 lines wrapping the cited primitive):

| Tool name | Wraps | Source |
|---|---|---|
| `create_vertex` | `HypergraphKernel.CreateVertex(VertexRoles)` | `[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:51-62]` |
| `set_vertex_status` | `SetDormant` / `Reactivate` | `[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:100-106]` |
| `add_incidence` | `HypergraphKernel.AddIncidence(source, member, byte, ordinal)` | `[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:116-126]` |
| `resolve_property` | `ResolveProperty<T>(name)` — registers a typed key, returns an opaque id | `[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:144-152]` |
| `set_property` / `get_property` / `remove_property` | the typed property trio | `[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:154-168]` |
| `get_vertex` / `count_vertices` | `TryGetVertex` / `CountVertices` | `[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:64-69,85]` |
| `is_reachable` / `shortest_path` / `bfs` / `connected_components` | the `IHypergraphQuery` default algorithms | `[verified:src=src/Topos.Hypergraph/IHypergraphQuery.cs:127-410]` |
| `directed_bfs` / `role_filtered_members` | the M9 `Knowledge` extensions | `[verified:src=src/Topos.Hypergraph.Knowledge/DirectedTraversal.cs:19-99]` |
| `semantic_recall` | `VectorIndex.NearestNeighbors` (only if embeddings are set) | `[verified:src=src/Topos.Hypergraph/VectorIndex.cs:30-41]` |

Deliberately **not** exposed in v1: `RestoreVertex` (Invariant-1 bypass, snapshot-only — exposing
it over MCP would be a footgun), `AllIncidences` (an internal-iteration helper, not a query),
`HasCycle` (returns `true` almost always on n-ary graphs — a documented footgun at the API layer,
worse over MCP where the agent can't read the warning `[verified:src=src/Topos.Hypergraph/IHypergraphQuery.cs:277-306]`).

---

## 5. The open forks (Nasser's calls — not pre-decided here)

These are the design questions a code-authoring session cannot resolve on its own. Each is framed
with the options and a tentative lean, but the lean is just a starting point for the decision, not
a recommendation dressed up as analysis.

### (a) Stateless v1 vs. stateful v1 — the biggest fork

- **Stateless** (each tool call is independent, no kernel persists across calls): trivially easy
  to build (~200 lines), but **almost useless for the actual use case** — agent memory requires
  state to persist across turns. A stateless server forces the agent to rebuild the graph every
  call, which defeats the point.
- **Stateful** (one kernel per session, persists across calls): what you actually want for memory,
  but forces real design decisions — lifecycle (when does a session kernel get torn down?),
  persistence (does it auto-snapshot via M4's `HypergraphSnapshot` on a timer? on a graceful-shutdown
  signal? `[verified:src=src/Topos.Hypergraph.Persistence/HypergraphSnapshot.cs:45-88]`), and
  multi-tenancy (one server, N agents — do they share a kernel or each get their own?).

**Tentative lean (not a decision):** ship **stateful v1, single-session, no auto-persistence** —
the simplest thing that's actually useful. The kernel lives for the lifetime of the server process;
the agent builds and queries it across turns; on process exit, state is lost (acceptable for a v1
demo; persistence is a v2 add-on that ties in cleanly via M4). Multi-tenancy is explicitly v3.
This keeps v1 small (~400 lines) while being genuinely usable, unlike stateless.

### (b) Transport: stdio, HTTP/SSE, or both?

- **stdio only** (v1): the canonical agent transport — Claude Code, Cursor, Continue all speak
  stdio. `[verified:web=https://github.com/modelcontextprotocol/csharp-sdk/blob/main/samples/QuickstartWeatherServer/Program.cs]`
  Simplest; the agent spawns the server as a subprocess.
- **HTTP/SSE** (v2 add-on via `ModelContextProtocol.AspNetCore`): needed for remote agents,
  browser-based clients, anything that can't spawn a local subprocess. Adds auth, CORS, hosting
  concerns.

**Tentative lean:** stdio only for v1. HTTP comes when a real consumer needs it (the same
forcing-function discipline the project applies everywhere else).

### (c) Package boundary: in this repo, or a separate repo?

- **In this repo** (`src/Topos.Hypergraph.Mcp/`): matches M4/M9 precedent, keeps everything in
  one place, easier to keep the server in sync with API changes. Risk: the repo accumulates
  consumer features and the line between "kernel" and "things built on the kernel" blurs.
- **Separate repo** (e.g. `Minu476/Topos.Mcp`): cleaner separation, the Topos repo stays a pure
  library, the server is clearly a consumer. Risk: version drift between server and kernel;
  two repos to maintain.

**Tentative lean:** in this repo for v1 (matches M4/M9, lower friction), revisit split if the
server grows substantially. Same call RLB could have faced and didn't — Topos-as-RLB-kernel stayed
in this repo, RLB references it from outside. `[verified:docs=docs/SPECIFICATION.md §6.1]`

### (d) Type-mapping: how does `Handle` cross the wire?

`Handle` is `readonly record struct Handle(uint Index, uint Generation = 0)` — a 64-bit value.
`[verified:src=src/Topos.Hypergraph/Handle.cs:17]` Options:

- **Opaque string** (`"#3"`, `"#3g1"` — matches `Handle.ToString()`): human-readable, agent can
  echo it back, but parsing is fuzzy. `[verified:src=src/Topos.Hypergraph/Handle.cs:36]`
- **JSON object** `{"index": 3, "generation": 0}`: precise, matches the struct, but verbose and
  the agent has to remember the shape.
- **JSON array** `[3, 0]`: compact, positional.

**Tentative lean:** opaque string (`"#3"`) — matches `Handle.ToString()` so it round-trips
naturally, and agents handle strings more gracefully than structured nested objects. Generation is
always 0 today, so the `g1` suffix never appears in practice. `[verified:src=src/Topos.Hypergraph/Handle.cs:11-15]`

### (e) Generic `PropertyKey<T>` ↔ wire: tagged union or untyped?

`SetProperty<T>` / `TryGetProperty<T>` are generic. `[verified:src=src/Topos.Hypergraph/HypergraphKernel.cs:144-168]`
Over MCP there's no static `T`. Options:

- **Tagged union per call**: `set_property(handle, name, type: "string"|"int"|"float[]"|..., value: ...)`.
  Explicit, type-safe, but verbose and the agent has to track types.
- **Untyped JSON**: `set_property(handle, name, value: <any JSON>)`, server infers `T` from the
  JSON shape. Simplest for the agent; relies on a JSON-shape→CLR-type mapping that's fragile
  (e.g. `int` vs `long`, `float` vs `double`).

**Tentative lean:** tagged union — explicitness wins for a typed-property library (the whole point
of `PropertyKey<T>` is type safety `[verified:src=src/Topos.Hypergraph/PropertyKey.cs:6-26]`),
and a verbose tool call is better than a silent type-coercion bug.

---

## 6. v1 scope (concrete enough for a code session to execute, contingent on §5 forks)

Assuming §5 leans hold (stateful single-session, stdio, in-repo, opaque Handle strings, tagged
property values), a v1 deliverable is:

1. New `src/Topos.Hypergraph.Mcp/` project. `PackageReference: ModelContextProtocol`.
   `ProjectReference: Topos.Hypergraph`, `Topos.Hypergraph.Knowledge`.
2. `ToposMcpServer.cs` — a `[McpServerToolType]` class with `[McpServerTool]` methods for the v1
   surface in §4 (~12 tools). Each method is a ~5-line wrapper around the cited primitive, with
   `[Description(...)]` on the method and every parameter (the SDK auto-generates the JSON schema
   from these). `[verified:web=https://github.com/modelcontextprotocol/csharp-sdk/blob/main/samples/QuickstartWeatherServer/Tools/WeatherTools.cs]`
3. `TypeMapping.cs` — `Handle` ↔ opaque string, role byte ↔ JSON int (or string for typed-role
   ergonomics), tagged-union property values ↔ JSON.
4. `Transport/StdioHost.cs` — the ~10-line `Program.cs` that wires up `AddMcpServer().WithStdioServerTransport().WithTools<ToposMcpServer>()`.
   `[verified:web=https://github.com/modelcontextprotocol/csharp-sdk/blob/main/samples/QuickstartWeatherServer/Program.cs]`
5. A session-scoped `HypergraphKernel` (one per server process for v1; multi-session is v2).
6. Tests under `tests/Topos.Hypergraph.Mcp.Tests/` — at minimum, round-trip tests for each tool
   (call `create_vertex` → get a Handle back → call `add_incidence` with it → call `bfs` → verify
   the result), using the SDK's `InMemoryTransport` (which exists in the SDK samples
   `[verified:web=https://github.com/modelcontextprotocol/csharp-sdk — samples/InMemoryTransport]`)
   so tests don't need a real agent.
7. A sample under `samples/Topos.Samples.McpAgent/` — a tiny agent config (e.g. a Claude Code
   `.mcp.json` entry `[verified:src=.mcp.json — existing format this matches]`) showing how to wire
   the server into a real agent. **This is the dogfooding gate:** the v1 exit criterion is "an
   actual MCP-aware agent can create a hyperedge and query it back, end-to-end, through this
   server." Same falsifiability standard M5 (ChatMemory) and M9 (RLB refactor) set.

### Exit criterion (the M5/M9 pattern)

A real MCP-aware agent (Claude Code, Cursor — whichever is easiest to configure locally)
successfully: creates vertices, builds an n-ary hyperedge, queries it with `role_filtered_members`,
and reads a property back — all through the MCP server, with zero C# written by the agent's author.
If the agent can't do that, v1 hasn't shipped. `[verified:docs=docs/SPECIFICATION.md §6 — M5 falsifiability gate pattern]`

### Estimated size

~400–600 lines of C#, of which the substantial fraction is the type-mapping and tool-method
boilerplate. Comparable to M9. A focused code session (Claude/Sonnet) plausibly ships v1 + tests in
one sitting, same as M9 did. `[verified:docs=docs/DECISIONS.md — "M9 IMPLEMENTED" entry, single-session build]`

---

## 7. What this milestone does *not* include (scope discipline)

- **No HTTP/SSE transport** — v2, gated on a real consumer needing remote agents.
- **No multi-tenancy** — v3, gated on a real consumer needing one server for N agents.
- **No auto-persistence** — v2, gated on a real consumer needing state to survive process restarts.
  M4's `HypergraphSnapshot` is the building block; the v2 work is wiring lifecycle hooks to call
  it. `[verified:src=src/Topos.Hypergraph.Persistence/HypergraphSnapshot.cs:45-88]`
- **No extraction/reasoning tools** — the server is a storage/query substrate, not an AI feature.
  Same scope boundary as the kernel itself.
- **No kernel changes.** If v1 surfaces a real kernel gap, that's a finding to report, not a change
  to make in M10. Same discipline M9 followed.
- **No NuGet publishing of the MCP package itself in v1.** The MCP package is a server (an
  executable consumers spawn, not a library they reference), so NuGet distribution is less central
  than for the kernel — but if it's wanted, it follows the same gated path as the kernel packages
  (`docs/NUGET_PUBLISH_CHECKLIST.md`).

---

## 8. Relationship to existing milestones

| Milestone | Relationship |
|---|---|
| **M0 (kernel)** | The MCP server is a pure consumer of M0's public API. No changes. |
| **M1 (query)** | `IHypergraphQuery` algorithms become MCP tools directly. |
| **M2 (reification)** | Reification is a usage pattern (`VertexRoles.Edge` + `AddIncidence`); the agent does it by composing tool calls. No special server support. `[verified:docs=docs/SPECIFICATION.md §7 pattern 12]` |
| **M3 (views)** | Views (`FilteredView`, `UnionView`) are not exposed in v1 — they're a C#-side abstraction that doesn't map cleanly to per-call MCP tools. A v2 could expose `subgraph_query` etc. |
| **M4 (persistence)** | The v2 auto-persistence feature leans on `HypergraphSnapshot`. `[verified:src=src/Topos.Hypergraph.Persistence/HypergraphSnapshot.cs]` |
| **M5 (embeddings/learnable)** | `VectorIndex.NearestNeighbors` → `semantic_recall` tool is the M5 surface over MCP. `LearnableEdge` / `EdgeStatistics` are not in v1 (an agent reinforcing edge weights per-call is a stretch). |
| **M6 (analytics)** | `LabelPropagation` / `TriangleCount` / `Modularity` could be exposed as analytics tools in a v2; not v1. `SWalk` (the hypergraph-specific one) is a natural candidate. |
| **M7 (spectral)** | Deferred, unrelated. |
| **M8 (API-stability)** | **The MCP server depends on M8's locked API surface.** M8 was explicitly the API freeze before any third-party consumer; the MCP server is exactly such a consumer. M8 had to be done first, and it is. `[verified:docs=docs/DECISIONS.md — "M8 CLOSED" entry]` |
| **M9 (Knowledge)** | `DirectedBfs` / `RoleFilteredMembers` / `AddIncidence<TRole>` become MCP tools. The M9 package's whole reason to exist — "where layer-1 judgment lives" — maps onto "the server exposes raw primitives, the agent supplies the judgment." `[verified:src=src/Topos.Hypergraph.Knowledge/DirectedTraversal.cs:10-16]` |

**M10 extends the locked M0–M9 structure; it doesn't reopen any of it.** Same discipline M9 followed
when it extended M0–M8. `[verified:docs=docs/DECISIONS.md — "M9 SCOPED" entry]`

---

## 9. Honest risks and counterarguments

Stated plainly, because a spec that only makes the case for itself isn't trustworthy:

1. **"Build it because it's cool" is not a forcing function.** The project has consistently deferred
   speculative features (M7 spectral, HIF interchange, docs site) until a real consumer forces them
   `[verified:docs=docs/DECISIONS.md — "M8 CLOSED" entry]`. The MCP server *looks* aligned with the
   thesis, but is there an actual agent-with-memory use case ready to consume it? If not, M10 joins
   the deferred pile. **This is the load-bearing question for §10.**

2. **An MCP server is a thing you have to maintain.** It tracks the MCP spec (currently 2025-11-25,
   will evolve), the SDK (v1.0 now, will have breaking changes), and Topos's own API. That's a real
   ongoing cost, not a one-time build.

3. **The agent-experience may be poor without a schema layer.** Raw `create_vertex` / `add_incidence`
   calls are low-level; an agent building a useful memory graph probably wants higher-level tools
   ("record this conversation turn with these entities") that encode domain patterns. Those are
   layer-1 concerns the *agent author* writes — but if every agent author has to write them, the
   server's value shrinks. Possibly a "Topos.Hypergraph.Mcp.Patterns" companion package of
   opinionated higher-level tools is the real product; v1's raw-primitive surface is the foundation,
   not the whole edifice.

4. **Type-mapping is fiddlier than it looks.** `PropertyKey<T>`'s generic-over-T shape was designed
   for compile-time type safety in C#; crossing to JSON loses that, and the tagged-union workaround
   in §5 (e) is verbose. Real consumers may chafe at it.

5. **M9's forcing evidence was three independent reinventions of the same pattern.** M10's forcing
   evidence is "the thesis implies it" — strong, but not the same as "three consumers already built
   this." `[verified:docs=docs/DECISIONS.md — "M9 SCOPED" entry, the three-consumers argument]`
   Worth being honest that this is a thesis-driven milestone, not an evidence-driven one like M9.

---

## 10. The decision Nasser needs to make

One question, with the options framed by §5:

> **Does the forcing-function case for an MCP server clear the bar this project sets for new
> milestones (a real consumer ready to use it, not just alignment with the thesis)?**

- **If yes** → approve M10 = MCP server, v1 scope per §6 (contingent on resolving the §5 forks;
  the leans above are a starting point). A code session executes it.
- **If "yes but not yet"** → defer M10 the same way HIF/docs-site/NuGet were deferred, with the
  re-entry condition logged ("returns when an actual agent-with-memory use case is ready to consume
  it"). `[verified:docs=docs/DECISIONS.md — "M8 SCOPE" entry, the deferral-re-entry-condition pattern]`
- **If no / not interested** → drop M10 from the roadmap. The MCP server idea lives in this doc as a
  record of consideration; the next milestone is something else (visualization, the GPT doc
  follow-ups, or whatever surfaces).

**The §5 forks (stateless/stateful, transport, package boundary, type-mapping, property
representation) are secondary** — they only matter if the answer is "yes." Resolve them after the
go/no-go, not before.

---

## 11. What I (GLM-5.2) did and did not do here

- **Did:** gathered evidence (source-verified the API surface, web-verified the MCP SDK state,
  cited the M5/M9 falsifiability pattern), framed the design space, stated the open forks with
  tentative leans, wrote a concrete-enough v1 scope for a code session to execute.
- **Did not:** flip any 🟡 to 🔒, declare M10 started, decide any of the §5 forks, write any code,
  or commit anything. Per `docs/GLM_DOCUMENTATION_GUIDELINES.md §4`, all of those are out of scope
  for this role. This document is for Nasser to read fresh and decide on.

---

## Sources

- `[verified:web=https://devblogs.microsoft.com/dotnet/release-v10-of-the-official-mcp-csharp-sdk/]` — Microsoft MCP C# SDK v1.0 announcement (supports 2025-11-25 spec).
- `[verified:web=https://github.com/modelcontextprotocol/csharp-sdk]` — official SDK repo.
- `[verified:web=https://github.com/modelcontextprotocol/csharp-sdk/blob/main/samples/QuickstartWeatherServer/Program.cs]` — host setup shape (`AddMcpServer().WithStdioServerTransport().WithTools<T>()`).
- `[verified:web=https://github.com/modelcontextprotocol/csharp-sdk/blob/main/samples/QuickstartWeatherServer/Tools/WeatherTools.cs]` — tool-definition shape (`[McpServerToolType]`, `[McpServerTool]`, `[Description]`).
- `[verified:web=https://csharp.sdk.modelcontextprotocol.io/]` — SDK docs. **License correction (2026-07-27):** this proposal originally claimed the SDK was MIT; it is **Apache-2.0** (verified via the NuGet package manifest during the M10 implementation pass). Apache-2.0 is compatible with Topos's chosen MIT license as a dependency, but the original `[verified:web]` tag above was wrong — corrected here, not silently edited, per the project's integrity discipline.
- `[verified:web=https://modelcontextprotocol.io/specification/2025-06-18]` — MCP specification (links to current 2025-11-25).
