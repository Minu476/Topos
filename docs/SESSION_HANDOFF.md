# Session Handoff — Topos (base-investigation phase complete)

**Last updated:** 2026-07-23
**Authored in:** ZCode (GLM-5.2)
**Purpose:** Comprehensive briefing so the next session (in Topos, possibly with a different
agent) can resume without re-deriving context. **This is the first document to read.**

If this handoff contradicts what you observe in the files, **trust the files** — and update
this handoff.

---

## 1. What Topos is (read this first if context is lost)

**Topos** (`/Users/nassertowfigh/Projects/Topos`) is a standalone, domain-agnostic
**typed-property hypergraph library for C#**, purpose-fit for AI / agent memory. The name
(*topos* = Greek for "place/location", root of *topology*) invokes the central thesis:
knowledge stored as topological graph structure rather than neural-network weights.

**Status: base-investigation phase complete. No code yet, by design.**

The library does not yet exist as code. What exists is the source-verified investigation
(`docs/BASE_INVESTIGATION.md`) that establishes *why* the library should exist, *what* to
borrow from existing systems, and a proposed *storage contract*. Two external reviews
(GPT, Fable) have arrived and been incorporated into `docs/DECISIONS.md`.

The next phase is: (a) write the missing competitor survey, (b) Nasser adjudicates one
strategic fork, (c) write the final spec, (d) start M0.

---

## 2. What's on disk right now

```
~/Projects/Topos/
├── README.md                         # public-facing intro
├── AGENTS.md                         # workspace instructions — read this on every session
├── .mcp.json                         # FSDE MCP wiring
├── .agents/mcp_config.json           # FSDE MCP wiring (mirror)
└── docs/
    ├── BASE_INVESTIGATION.md         # ✅ REVISED this session (a/b/c applied)
    ├── DECISIONS.md                  # ✅ NEW this session — what's locked vs. open
    ├── SESSION_HANDOFF.md            # this file
    └── reactions/
        ├── 01_GPT_first-reaction.md       # GPT's review (verbatim)
        └── 02_Fable_first-reaction.md      # Fable's review (verbatim)
```

**No code. No tests. No .git.** Topos sits in the working tree of the parent `~/Projects`
repo, untracked. First commit / `git init` is Nasser's to make (see §7 below).

---

## 3. What this session did (so the next session doesn't redo it)

1. **Created Topos as a standalone project** (separate from Rich-Learning-Base, which stays
   untouched). Named it *Topos* — Nasser approved the name.
2. **Wrote `docs/BASE_INVESTIGATION.md`** — source-verified analysis of 10 libraries:
   HyperNetX, DHG, yamafaktory/hypergraph (Rust), SimpleHypergraphs.jl, KaHyPar, JGraphT,
   RDF 1.2, Kuzu (with Apple-acqui-hire correction), TypeDB, EnTT. Every claim tagged
   `[verified:src=…]` / `[verified:spec=…]` / `[verified:web=…]` / `[unverified:inferred]`.
3. **Saved two external reviews verbatim** in `docs/reactions/` — GPT (01) and Fable (02).
4. **Revised BASE_INVESTIGATION.md (a/b/c)** per the agreed reviewer feedback:
   - (a) Downgraded the Apple/Kuzu pillar from "thesis validation" to "weak-positive signal."
   - (b) Adopted GPT's "the workload changed" framing in §1 (was "AI requires a new graph library").
   - (c) Applied Fable's `float[]`-not-`double[]` fix and added the measured-benchmark M0 gate.
5. **Wrote `docs/DECISIONS.md`** — synthesis of the two reviews: 5 questions settled by
   consensus, 1 divergent (M5 sequencing — lean toward Fable's "split it"), 1 strategic fork
   (decouple-vs-RLB-kernel — Nasser's call, blocks the spec).
6. **Wrote this handoff + AGENTS.md + FSDE wiring.**

---

## 4. The three things that matter most for the next session

### 4.1 The biggest open gap (Fable identified it)

**The investigation surveyed hypergraph libraries but not agent-memory competitors.** The
systems actually competing for the "AI agent memory substrate" niche — **Zep/Graphiti,
mem0, Letta, Cognee** — are property-graph or temporal-binary-graph systems, not hypergraphs.
All chose binary graphs.

**The unanswered feasibility question:** *is the hypergraph gap unfilled because nobody
built it, or because the field tried hypergraphs and decided binary was good enough for
agent memory?*

**Next action: write `docs/AGENT_MEMORY_COMPETITORS.md`.** Source-verified survey of those
four systems against the same 9-point schema used in BASE_INVESTIGATION. Same integrity
standard (`[verified:src=…]` tags). This is the investigation's biggest gap and it's the
first thing a skeptic will attack.

The counter-argument to "binary is good enough" lives in Rich-Learning-Base's own evidence
(the paradox-compression finding, the deferred-HyperEdge trigger): n-ary composition with
measured non-derivable payloads cannot be faithfully expressed in binary edges without lossy
encoding. **That empirical argument — not the library survey — should open the final spec.**
(Fable's strongest point.)

### 4.2 The strategic fork — RESOLVED (build as RLB's kernel first)

**Decision (Nasser, 2026-07-23): build Topos as RLB's kernel first**, with standalone-library
ambition as a falsifiable M5 milestone (a non-RLB second consumer). This was Fable's
recommendation; Nasser adopted it. See `docs/DECISIONS.md` §6 for the full record.

**What this changes for the next session:**
- **RLB is now in scope.** Topos becomes a `ProjectReference` in RLB's V2 csproj during
  M0–M4. RLB's 337-test suite becomes the first real consumer.
- **The M5 exit criterion is now a non-RLB second consumer** (e.g. a minimal chat-agent
  memory demo). This is the falsifiable test of domain-agnosticity.
- **Dependency direction is preserved:** Topos references nothing upstream (no RLB types
  leak into Topos); RLB references Topos. The kernel stays clean.
- **RLB is touched during this build** — the earlier "RLB stays untouched" framing in older
  handoff sections is now superseded.

### 4.2b Verification strategy — Neo4j GDS as the correctness oracle (NEW)

**Decision (Nasser, 2026-07-23): use Neo4j GDS as an independent oracle for Topos's
algorithms.** The Lean-for-NexusVerifier pattern applied to graph algorithms. See
`docs/DECISIONS.md` §6 and `AGENTS.md` §9.

- For each standard algorithm Topos ships (BFS/DFS/shortest-path/cycle/SCC/PageRank/
  community detection), a paired test runs the same query against the same graph in Neo4j
  GDS and asserts outputs match.
- GDS is the oracle for *standard* algorithms only. For hypergraph-specific algorithms
  (s-walk, role-gated reachability, anchor/condition/target semantics), GDS can't verify —
  Topos's answer is the novel claim there.
- **M1's exit criterion now includes a GDS-parity test suite.** M6 (analytics) gets a
  strong verification path (GDS ships Louvain, Label Propagation, WCC/SCC, Triangle
  Counting, Local Clustering Coefficient as direct oracles).
- Neo4j is already in Nasser's stack. The GDS plugin install is the only addition.

### 4.3 The frozen storage contract (proposed, mostly settled)

4 primitives + 2 invariants. Survived every attempt to add a fifth. The two reviewers
settled 5 of the 7 open questions about it (see `docs/DECISIONS.md` §1):

```
PRIMITIVES (4):
    Handle        — newtype + monotonic never-reused counter + generational version bits
    Vertex        — Handle + VertexRoles (bitmask) + VertexStatus (reserved hot-path slot)
                    + PropertyBag (columnar, EnTT-style sparse-set pools)
    Incidence     — SourceHandle + MemberHandle + IncidenceRole (byte) + Ordinal (packed struct)
    PropertyKey<T>— identity (string) separate from PropertyId (int, per-process registry)

INVARIANTS (2):
    1. Dormant never garbage-collected; provenance edges always resolve.
    2. VertexRoles and IncidenceRole are independent axes.
```

The 5 settled answers: reserved hot-path slots YES; spectral DEFERRED; packaging split at M4;
reification depth NO CAP; embeddings as `PropertyKey<float[]>` not first-class field. The
divergent one: M5 sequencing (lean toward "split it — shapes early, machinery in M5").

---

## 5. What to do when the next session starts (decision tree)

**Read AGENTS.md and this handoff first. Then:**

- **If Nasser says "write the competitor survey"** → write `docs/AGENT_MEMORY_COMPETITORS.md`.
  Fan out Explore agents on Zep/Graphiti, mem0, Letta, Cognee. Same 9-point schema, same
  `[verified:src]` discipline. Output one markdown doc.
- **If Nasser says "I've decided the strategic fork"** → record it in `docs/DECISIONS.md` §6
  (decision log format), update §3.1 to reflect the decision, then unblock the spec.
- **If Nasser says "write the spec"** → only after (a) the competitor survey exists and
  (b) the strategic fork is decided. Spec lives at `docs/SPECIFICATION.md`, opens with the
  RLB paradox-compression empirical argument (Fable's point), incorporates the 4-primitive
  contract, the 5-layer architecture (GPT), the resolved open questions, and the M0–M8
  roadmap with the measured-benchmark M0 gate.
- **If Nasser says "start M0"** → don't, until the spec exists. M0 without a spec is
  premature; the contract is proposed, not frozen.
- **If Nasser asks about RLB** → RLB is untouched and stable. The 337-test V2 suite is green
  without any of this. Topos does not reference RLB. Do not couple them without Nasser's
  explicit decision on the strategic fork.

---

## 6. Honest caveats for the next session

1. **Don't re-litigate the contract without reason.** Four rounds of pressure-testing (you,
   ChatGPT, Sonnet, GPT) plus two external reviews converged on 4 primitives + 2 invariants.
   Every attempt to add a fifth failed. If you find a real reason to add one, say so — but
   the bar is high.
2. **Don't promote the Apple/Kuzu acquisition beyond "weak-positive signal."** This was an
  integrity slip in the original draft (footnoted as reporting, then promoted to thesis
  validation). It's been downgraded in §1. Apple has not stated the motive.
3. **Don't start M0 before the spec.** The contract is *proposed*, not frozen. Code now
   would lock decisions that should be made in the spec with Nasser's adjudication.
4. **Don't forget the missing competitor survey.** It's the investigation's biggest gap and
   the first thing a skeptic attacks. The library survey doesn't answer "why did the
   agent-memory field choose binary graphs?" — only the competitor survey + RLB's empirical
   evidence can.
5. **Maintain the `[verified:src]` discipline.** Every claim about an external system must
   be source-traceable. `[unverified:inferred]` for reasoned claims. No unsourced assertions.
   This is the integrity substrate that makes the document trustworthy.
6. **Two prior investigations established no production C# hypergraph exists.** Don't
   re-derive that. The investigation went deeper into design patterns; the conclusion stands
   and is source-backed.
7. **Topos is not yet a git repo.** It's in the parent `~/Projects` repo's working tree,
   untracked. First commit is Nasser's to make (see §7).

---

## 7. Open practical items for Nasser

- **Git:** Topos needs its own `git init` (it's currently untracked in the `~/Projects`
  parent repo). Three options were offered last session: (1) `git init` inside Topos + add
  to parent's `.gitignore` [recommended], (2) leave untracked, (3) tell me what the parent
  repo is for. Awaiting your call. No git changes will be made without your say-so.
- **The Medium piece on Kuzu** ("I Analyzed 163K Lines of Kuzu's Codebase — Here's Why
  Apple Wanted It") returned HTTP 403 to my fetch. It likely has the deepest public
  architectural analysis of Kuzu's storage engine. Worth your reading directly before M4
  is finalized.
- **The strategic fork (§4.2)** is yours to adjudicate. It blocks spec finalization.

---

## 8. Provenance and integrity note

This handoff was authored in ZCode by GLM-5.2. Every claim about the investigation's content
is verifiable in `docs/BASE_INVESTIGATION.md` (with provenance tags). Every claim about the
reviews is verifiable in `docs/reactions/`. Every claim about what's decided is verifiable in
`docs/DECISIONS.md`. If anything here contradicts those files, trust the files and update this
handoff.

The investigation's integrity standard (source-grade verification, no unsourced assertions)
is borrowed from NexusVerifier's `SOLVED_PROBLEMS.md` (verified vs. axiom-scaffolded vs.
`sorry`). Maintain it.
