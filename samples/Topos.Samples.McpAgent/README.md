# Topos.Samples.McpAgent

M10's dogfooding entry point (`docs/MCP_SERVER_SPEC.md` §6 item 7). Not a C# project — a config
sample showing how to wire `src/Topos.Hypergraph.Mcp/` into a real MCP-aware agent (Claude Code,
Cursor, Continue).

## Use it with Claude Code

1. Build the server once in Release mode: `dotnet build src/Topos.Hypergraph.Mcp -c Release`
   from the repo root.
2. Copy `.mcp.json.example` in this directory to `.mcp.json` at the repo root (merge with the
   `mcpServers` entry already there for `fsde` if you keep both), or point an existing `.mcp.json`
   at `src/Topos.Hypergraph.Mcp/Topos.Hypergraph.Mcp.csproj` the same way.
3. Start (or restart) Claude Code from the repo root. It spawns the server as a subprocess over
   stdio — the same transport `fsde`'s entry in `.mcp.json` already uses.
4. Ask the agent to create a couple of vertices, add an incidence between them, and call
   `bfs`/`role_filtered_members` to read the graph back. No C# needs to be written for any of
   this — that round trip is the v1 exit criterion (spec §6, "Exit criterion").

## What's in the tool surface

See `src/Topos.Hypergraph.Mcp/ToposMcpServer.cs` for the full list — vertex/incidence CRUD,
typed property get/set/remove (tagged union: `"string"`/`"number"`/`"bool"`/`"embedding"`),
kernel-level query (`is_reachable`, `shortest_path`, `bfs`, `connected_components`), M9's
role-aware directed traversal (`directed_bfs`, `directed_shortest_path`, `role_filtered_members`),
and `semantic_recall` over embedding properties.

## State lifetime

Stateful, single-session (spec §5 fork (a), approved lean): one `HypergraphKernel` lives for the
server process's lifetime. Everything you build is lost when the process exits — there's no
auto-persistence in v1. Restarting the agent (which respawns the subprocess) starts a fresh, empty
graph.
