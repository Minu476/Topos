# AGENTS.md — Topos

Guidance for any agent (ZCode/GLM, Claude Code, Cursor, Continue, Kimi, Antigravity/Gemini)
working in this repository. **Read this first on every session.**

---

## 1. What Topos is

**Topos** is a standalone, domain-agnostic **typed-property hypergraph library for C#**,
purpose-fit for AI / agent memory — LLM reasoning, explainability, learnable edges, tiered
memory, and provenance.

> *Topos* (Greek τόπος, "place / location") is the root of *topology*. The name invokes the
> central thesis: **knowledge stored as topological graph structure rather than neural-network
> weights.** In category theory, a *topos* is a deep theory of structured contexts — a fitting
> ambition for a foundational substrate that many domains build on.

**Current status: base-investigation phase.** No code yet, by design. The base investigation
(`docs/BASE_INVESTIGATION.md`) is the input to the final specification, which Fable and GPT
are reviewing. See `docs/SESSION_HANDOFF.md` for the precise carry-on state.

The intended eventual namespace is `Topos.Hypergraph` — *subject to change* if GPT's deeper
reframe ("Topos is an incidence model with hypergraph as one projection") is adopted; see
`docs/DECISIONS.md`.

---

## 2. The thesis Topos exists to serve (and the harder question still open)

The age of AI **changed the optimization criteria** for a graph library — it did not invent a
new kind of graph. Historical graph libraries optimized for shortest paths, centrality,
partitioning, spectral decomposition, community detection. The AI-memory workload optimizes
for incremental updates, provenance, explainability, retrieval, symbolic+vector coexistence,
stable identities, partial activation, long-lived mutable knowledge. These are genuinely
different workloads, and no hypergraph library in any language is built for the second set.

**The harder question still open** (see `docs/BASE_INVESTIGATION.md` §8.3): the systems
actually competing for the "AI agent memory substrate" niche (Zep/Graphiti, mem0, Letta,
Cognee) are *not* hypergraph libraries — they chose binary property graphs or temporal binary
graphs. So the real feasibility question is *"is the gap unfilled because nobody built it, or
because the field tried hypergraphs and decided binary was good enough?"* That requires a
separate survey (`docs/AGENT_MEMORY_COMPETITORS.md` — **not yet written; this is the next
document**) plus the empirical counter-argument from RLB's paradox-compression finding.

---

## 3. The frozen storage contract (proposed, under review)

The four-primitive + two-invariant shape, synthesized from source-level reading of 10
libraries. This is a *proposal* for the spec — not a final decision — but it survived every
attempt to add a fifth primitive, which is a real signal.

```
PRIMITIVES (4):
    Handle        — newtype + monotonic never-reused counter + generational version bits
    Vertex        — Handle + VertexRoles (bitmask) + VertexStatus (reserved hot-path slot)
                    + PropertyBag (columnar, EnTT-style sparse-set pools)
    Incidence     — SourceHandle + MemberHandle + IncidenceRole (byte) + Ordinal (packed struct;
                    cell properties attach here)
    PropertyKey<T>— identity (string) separate from PropertyId (int, per-process registry)

INVARIANTS (2):
    1. Dormant never garbage-collected; provenance edges always resolve (even to dormant targets).
    2. VertexRoles and IncidenceRole are independent axes.
```

Read `docs/BASE_INVESTIGATION.md` §5 for the full proposal and the patterns each primitive
borrows from (Rust crate / EnTT / HyperNetX / TypeDB / RDF 1.2). Read `docs/DECISIONS.md` for
which parts of the contract the two reviewers locked vs. left open.

---

## 4. Critical relationships (strategic fork RESOLVED — build as RLB's kernel first)

**Decision (Nasser, 2026-07-23): build Topos as RLB's kernel first**, with standalone-library
ambition as a falsifiable M5 milestone (a non-RLB second consumer). See `docs/DECISIONS.md` §6.

What this means concretely:
- **Rich-Learning-Base** (`~/Projects/Rich-Learning-Base`) — **now in scope as the first
  consumer.** Topos becomes a `ProjectReference` in RLB's V2 csproj during M0–M4. RLB's
  337-test suite becomes the first real validation. **RLB is touched** during this build —
  unlike the earlier decoupled plan.
- **FSDE** (`~/Projects/FSDE`) — still decoupled for now. May adopt Topos later.
- **Dependency direction preserved:** Topos still references nothing upstream (no RLB types
  leak into Topos); RLB references Topos. The kernel stays clean.
- **M5 exit criterion is now a non-RLB second consumer** (e.g. a minimal chat-agent memory
  demo). If the kernel can't serve a consumer it wasn't designed around, the
  "standalone library" claim isn't yet true — better to learn at M5 than M8.

---

## 5. At session start — pull your briefing

**On launch, call these in order before doing anything else** (when the FSDE MCP tools are
loaded; if not, use the `fsde` CLI as a fallback):

1. **`fsde_start_session`** with `projectPath=/Users/nassertowfigh/Projects/Topos`. Returns
   the session briefing.
2. **`fsde_read_directives`** (recipient=agent, status=open) — pick up directives.
3. **`fsde_get_todos`** — the live todo list. The markdown files in `docs/` are the
   human-readable source of truth; Neo4j catches up via re-ingestion.

If FSDE MCP tools are NOT loaded in the session, the `fsde` CLI works:
`fsde get-context --dir /Users/nassertowfigh/Projects/Topos`.

**Either way, always read `docs/SESSION_HANDOFF.md` first** — it carries the carry-on state
across sessions and is authoritative when FSDE is cold.

---

## 6. Before design/code claims — ground in source

- **`fsde_find_concept`** — when a task touches a named concept (reification, fossilization,
  incidence, role semantics, tiered memory), look it up.
- **`fsde_context_for_code`** — before changing or proposing a file/method, pull its context.
  Hint, not oracle — verify against the real file.
- **For library claims:** the investigation's integrity standard is source-grade. Every claim
  is tagged `[verified:src=path]`, `[verified:spec=section]`, `[verified:paper=ref]`,
  `[verified:web=url]`, or `[unverified:inferred]`. **Continue this discipline.** No unsourced
  assertions. This mirrors NexusVerifier's `SOLVED_PROBLEMS.md` (verified vs. axiom-scaffolded
  vs. `sorry`).

---

## 7. At session end — write back so the next session inherits

- **`fsde_end_session`** with a summary of what was done, decisions made, what's next.
- **`fsde_log_work_event`** for anything the next session needs to know.
- **`fsde_set_objective`** if the goal shifted.
- **Update `docs/SESSION_HANDOFF.md`** so the next session opens briefed, not cold.

---

## 8. The repository layout (as of session start)

```
~/Projects/Topos/
├── README.md                         # public-facing intro
├── AGENTS.md                         # this file
├── .mcp.json                         # FSDE MCP wiring
├── .agents/
│   └── mcp_config.json               # FSDE MCP wiring (mirror)
└── docs/
    ├── BASE_INVESTIGATION.md         # the 10-library source-verified analysis + contract
    ├── AGENT_MEMORY_COMPETITORS.md   # the Zep/mem0/Letta/Cognee survey + n-ary-DB matrix
    ├── SPECIFICATION.md              # the consolidated spec — under GPT+Claude review
    ├── DECISIONS.md                  # what reviewers locked vs. left open
    ├── SESSION_HANDOFF.md            # carry-on context — READ FIRST
    └── reactions/
        ├── 01_GPT_first-reaction.md       # GPT's first review
        ├── 02_Fable_first-reaction.md      # Fable's first review
        └── 03_*.md                          # (pending) GPT+Claude review of the SPECIFICATION
```

**No code yet.** The first code milestone is M0 (storage kernel) — but do not start M0 until:
(a) the strategic fork in §4 is adjudicated, and (b) the agent-memory competitor survey
(`docs/AGENT_MEMORY_COMPETITORS.md`) is written. See `docs/SESSION_HANDOFF.md`.

---

## 9. Verification strategy — Neo4j GDS as the correctness oracle

**Decision (Nasser, 2026-07-23): use Neo4j GDS (Graph Data Science library) as an independent
oracle for Topos's algorithms.** This is the Lean-for-NexusVerifier pattern applied to graph
algorithms: a trusted external implementation as ground truth. See `docs/DECISIONS.md` §6.

What this means concretely:
- For each standard algorithm Topos implements (BFS/DFS/shortest-path/cycle/SCC/PageRank/
  community detection), a paired test runs the same query against the same graph in Neo4j
  GDS and asserts the outputs match.
- Property-graph projection of a hypergraph (via the ProjectionEngine from
  BASE_INVESTIGATION §5.4) gives a binary-graph view GDS can operate on — the standard
  algorithms verify cleanly through this projection.
- For algorithms where the hypergraph and its projection disagree (s-walk, role-gated
  reachability, anchor/condition/target semantics), GDS can't verify — Topos's answer is
  the novel claim there. GDS is the oracle for the *standard* algorithms only.
- **M1's exit criterion now includes a GDS-parity test suite.** M6 (analytics — Louvain,
  Label Propagation, WCC/SCC, Triangle Counting, Local Clustering Coefficient) gets a strong
  verification path: GDS ships all of these as direct oracles.
- Neo4j is already in Nasser's stack (RLB's `Neo4jGraphMemory`, FSDE). No new infrastructure
  — the GDS plugin install is the only addition. Test harness: a `Topos.Tests.GdsOracle`
  project/class added during M1.

---

## 10. Honest caveats

- **No code, no tests, no benchmarks yet.** All performance claims in the investigation are
  inferred from storage representation, not measured. M0's exit criterion requires *measured*
  benchmarks (added per reviewer feedback).
- **The Apple/Kuzu acquisition is weak-positive signal, not thesis validation.** Apple has
  not stated the motive; the "on-device AI" angle is one analyst's read, not Apple's
  statement. Downgraded in §1 of the investigation per reviewer Fable.
- **Topos is not yet a git repository.** It sits in the working tree of the parent
  `~/Projects` repo, untracked. First commit / `git init` is Nasser's to make.
- **The specification phase is complete.** `docs/SPECIFICATION.md` is **approved by both GPT and
  Claude** for implementation (GPT: 9–10/10, "no further changes requested"; see
  `docs/reactions/03–05`). The next phase is **M0 (storage kernel)** — see `SPECIFICATION.md §6`.
  Two open questions (Q1: the "paradox-compression" citation; Q7: Handle generation-bits) most
  affect M0 code shape and should be resolved before/as M0 starts; the rest defer to their
  milestone. **Do not keep polishing the spec** — both reviewers warn against the
  overengineering risk.
- **RLB is now in scope** (build-as-RLB-kernel decision). The earlier "RLB stays untouched"
  framing in older docs is superseded — see `docs/DECISIONS.md` §6.
