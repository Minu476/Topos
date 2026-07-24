# Agent-Memory Competitors — the missing survey
### Source-verified analysis: did the field reject hypergraphs, or never consider them?

**Author:** ZCode (GLM-5.2) · **Date:** 2026-07-23
**Purpose:** This is the investigation's biggest gap (flagged in `BASE_INVESTIGATION.md` §8.3 by
reviewer Fable). The base investigation surveyed *hypergraph* libraries and proved the
hypergraph gap exists. It did **not** survey the systems actually competing for the "AI agent
memory substrate" niche — **Zep/Graphiti, mem0, Letta, Cognee** — all of which chose binary
graphs (or non-graph representations), not hypergraphs. The feasibility question this document
must answer is:

> **Is the hypergraph gap unfilled because nobody built it, or because the field tried
> hypergraphs and decided binary was good enough for agent memory?**

**Integrity standard:** Every claim is tagged `[verified:src=url]`, `[verified:docs=url]`,
`[verified:web=url]`, or `[unverified:inferred]` — same discipline as
`BASE_INVESTIGATION.md`. The four competitor investigations were run in parallel as source-grade
research agents that read actual repo code, schemas, and migrations (not READMEs). This
document is the synthesis. Each system's full source-graded report is folded into §3 below;
the comparison matrix in §4 is the load-bearing artifact.

---

## 1. The thesis this document tests — stated before the evidence

Topos is a proposed typed-property **hypergraph** library for C# purpose-fit for AI/agent
memory. The base investigation (`docs/BASE_INVESTIGATION.md`) proved, three times over, that no
hypergraph library in any language targets the AI-memory workload. But a skeptic has a sharper
attack than "is the hypergraph gap real?":

> *"The systems that actually won the agent-memory niche (Zep/Graphiti, mem0, Letta, Cognee) all
> chose binary property graphs or pure vector stores. Maybe hypergraphs were considered and
> found wanting. Maybe binary genuinely is good enough for agent memory, and the gap is unfilled
> because filling it adds no value."*

This is the attack the library survey cannot answer. Only a source-grade reading of what the
incumbents actually built — and, critically, **where their binary/vector choice costs them
expressiveness** — can answer it. That is what this document does.

**The answer, previewed:** The field did **not** try hypergraphs and reject them. There is zero
evidence in any of the four codebases of a hypergraph ever being considered, prototyped, or
discussed. The binary-graph (or vector-store) choice is an **unexamined default inherited from
the property-graph DB lineage** — and in every system there are specific, identifiable places
where that default actively costs expressiveness (n-ary facts, cell-level properties, reified
facts-as-entities, persisted learnable weights). The gap is unfilled because **nobody built
it** — and the moment relational expressiveness becomes load-bearing, the incumbents either
work around it lossily (Graphiti, Cognee) or retreat from the relational layer entirely (mem0).

---

## 2. The four systems surveyed

| # | System | Language | Category | What it actually is (source-verified) | Status |
|---|---|---|---|---|---|
| 1 | **Zep / Graphiti** | Python | Temporal knowledge graph | Binary property graph (Neo4j/FalkorDB/Neptune/Kuzu) + episodic provenance + validity intervals. The most graph-mature of the four. | Apache-2.0, ~29k★, very active `[verified:web]` |
| 2 | **mem0** | Python | "Memory layer for AI agents" | **Was** a binary-edge property graph (Neo4j); the graph layer was **deleted from OSS on 2026-04-14** (`a488e19044e4`). Current OSS = vector store + entity-tag vector store + additive scoring. | Apache-2.0, ~61k★, very active `[verified:src]` |
| 3 | **Letta** (formerly MemGPT) | Python | Tiered agent memory | Editable core-memory **blocks** + message **log** + **vector store** (pgvector/Turbopuffer). **No graph layer exists** in the codebase (exhaustive grep, zero hits). The tiered-memory champion. | Apache-2.0, ~24k★, active `[verified:src]` |
| 4 | **Cognee** | Python | Knowledge-graph + vector platform | Real binary property graph (7 backends: Neo4j/FalkorDB/Neptune/Kuzu/Turso/PG-AGE/"Ladybug") + parallel vector store. The most backend-diverse; genuinely graph-structured. | Apache-2.0, ~29k★, very active `[verified:src]` |

**Two clarifications the survey surfaced that matter more than expected:**

- **mem0 is no longer a graph system.** The April-2026 pivot deleted the entire Neo4j graph
  layer from OSS and replaced it with "single-pass ADD-only extraction" + vector entity
  boosting. `[verified:src=gh:commits/a488e19044e4]` Even the platform's "native graph" is
  documented as co-occurrence-only ("it won't record a 'manages' edge... connections are
  inferred from co-occurrence rather than declared").
  `[verified:docs=docs/platform/features/graph-memory.mdx:55]` So mem0 is best read as
  evidence of *retreat* from the relational layer — see §5.

- **Letta was never a graph system.** The "Letta is adding graph features recently" premise
  does **not** hold up against the source. Exhaustive grep for `knowledge_graph`, `class.*Graph`,
  `hyperedge`, `edge_type`, `entity_relation`, `relational_memory`, `memory_graph` returned
  **zero hits** across all `.py` files, the README, and the `fern/` docs tree. The one new typed
  construct, `Identity`, is a property-bag *node* with **no edges**. Letta's design is a
  coherent philosophy (the LLM is the reasoner; memory is legible text it reads/edits), not a
  graph that fell short.

This narrows the field: of the four named "agent-memory graph" competitors, **only Graphiti
and Cognee are genuinely graph-structured today**, and both are strictly binary.

---

## 3. Per-system summaries

### 3.1 Zep / Graphiti (getzep/graphiti) — the most graph-mature incumbent

- **Status:** Apache-2.0, 29,119★, last commit 2026-07-23 (today), arXiv paper
  (2501.13956 "Zep: A Temporal Knowledge Graph Architecture for Agent Memory"), MCP server,
  SDKs. `[verified:web]`
- **⚠ READ THIS FIRST — Graphiti's own paper claims "hyper-edges" (the single most dangerous
  quote a skeptic will throw at this survey).** The Zep paper (arXiv 2501.13956) says Graphiti
  *"model[s] complex multi-entity facts through an implementation of hyper-edges."*
  `[verified:web=arxiv.org/html/2501.13956v1]` **This is rhetorical, not structural.** The same
  paper's formal edge definition is strictly binary — `e_i ∈ E_s ⊆ φ*(N_s × N_s)`, i.e. edges
  are a binary relation over *pairs* of nodes. The fact-extraction prompt enforces *"Each fact
  should represent a clear relationship between two DISTINCT nodes"* and edge deduplication is
  *"constrained to edges existing between the same entity pairs."* The backend is Neo4j's
  binary property-graph. So Graphiti's "hyper-edge" is **reified-binary** — a multi-entity
  concept is *decomposed into several pairwise binary edges*, not stored as one n-ary relation.
  `[verified:web=arxiv.org/html/2501.13956v1]` This is exactly the lossy decomposition
  Topos proposes to eliminate; it is not a counterexample to the binary thesis. **The skeptic
  who quotes "hyper-edges" at you must be answered with the `N_s × N_s` definition.**
- **Storage:** Pluggable property-graph backend via `GraphProvider` enum:
  `NEO4J | FALKORDB | KUZU | NEPTUNE`. **Kuzu is deprecated** ("upstream Kuzu project is no
  longer maintained... will be removed in a future release") — note: this is the *same* Kuzu
  whose acqui-hire by Apple is the weak-positive signal in `BASE_INVESTIGATION.md` §3.8.
  `[verified:src=graphiti_core/driver/driver.py — class GraphProvider]`
- **Identity:** Random `uuid4` string per node/edge. **Not content-addressed** — re-running
  extraction over the same source yields new UUIDs and relies on LLM-based
  `dedupe_nodes.py`/`dedupe_edges.py` to reconcile. No monotonic counter, no stable handle
  across re-ingestion. `[verified:src=graphiti_core/nodes.py:94; prompts/dedupe_*.py]`
- **Edge model — strictly binary:** The `Edge` ABC declares exactly two endpoints
  (`source_node_uuid`, `target_node_uuid`). Five edge subtypes (EpisodicEdge, EntityEdge,
  CommunityEdge, HasEpisodeEdge, NextEpisodeEdge) — all binary.
  `[verified:src=graphiti_core/edges.py:44-46]`
- **The temporal model (Graphiti's real differentiator):** Every `EntityEdge` carries up to
  four timestamps plus provenance — `valid_at`, `invalid_at` (interval = [valid_at, invalid_at)),
  `expired_at`, `reference_time`, and `episodes: list[str]` (the source episode UUIDs). This
  is how Graphiti answers "what was true then" queries — a genuine temporal-graph capability
  that flat-vector RAG lacks. `[verified:src=graphiti_core/edges.py:247-272]`
- **Embeddings unified:** Yes — `name_embedding` on entities and `fact_embedding` on edges are
  stored as graph properties alongside the symbolic fields, with provider-specific vector
  coercion. Not a separate vector store. `[verified:src=nodes.py:500; edges.py:249]`
- **The reification smoking gun (provider-internal only):** Because Kuzu's edge-property
  support is insufficient, Graphiti stores entity edges *as nodes* with label `RelatesToNode_`,
  wired between two `:RELATES_TO` relationships. Node-delete code comments: *"Entity edges are
  actually nodes in Kuzu, so simple DETACH DELETE will not work."* This is provider-internal
  reification — the conceptual model and LLM-facing schema remain strictly binary.
  `[verified:src=graphiti_core/models/edges/edge_db_queries.py — KUZU case]`
- **Dead ends (where binary costs Graphiti):**
  1. **N-ary facts are forbidden by the schema AND the extraction prompt — and the prompt
     *actively coerces* otherwise-unary facts into binary.** The extraction prompt's `Edge`
     model has exactly `source_entity_name` + `target_entity_name`, and RULE 2 states *"Each
     fact must involve two distinct entities."* Rule 3 goes further: when a sentence describes
     a detail about a single entity, the prompt instructs the LLM to *"look for a second entity
     in the ENTITIES list that the detail relates to and form a proper triple"* — so the
     system won't even represent a unary fact as-is; it hunts a second endpoint. Co-authorship,
     multi-party transactions, group memberships-as-events: all structurally unrepresentable
     and forced into cliques or stars.
     `[verified:src=graphiti_core/prompts/extract_edges.py — Edge model + RULE 2 & 3]`
  2. **No reification at the model layer.** You cannot link *to* a fact; you can only
     timestamp/supersede it via `invalid_at`/`expired_at` + a new edge.
  3. **Cell-level properties don't exist** — only node-level and edge-level
     `attributes: dict[str,Any]`. Per-participant roles/orderings/contributions have no home.
  4. **No persisted learnable edge weights** — ranking is recomputed at query time by
     cross-encoder/RRF/MMR rerankers, then discarded. A reinforcement-style memory has no
     persisted scalar to update.
- **Verdict:** The strongest incumbent. Temporal intervals + episode provenance + unified
  embeddings are a genuinely good design for pairwise facts. The hypergraph gap is felt
  specifically at n-ary facts, cell properties, and reified facts-as-entities.

### 3.2 mem0 (mem0ai/mem0) — the retreat from graphs

- **Status:** Apache-2.0, 61,552★, very active (YC S24, managed platform).
  `[verified:web]`
- **The headline finding:** **mem0 deleted its entire graph layer from OSS on 2026-04-14.**
  Commit `a488e19044e4` "feat(oss): port v3 pipeline" removed Neo4j/Memgraph/Kuzu/AGE/Neptune
  support and replaced it with "single-pass ADD-only extraction... nothing is overwritten" +
  vector entity boosting. `[verified:src=gh:commits/a488e19044e4]`
- **Current storage (v3, post-pivot):** `MemoryConfig` has only `vector_store`, `llm`,
  `embedder`, `history_db_path`, `reranker`, `version`. **No `graph_store` field.** The "graph"
  is now a second vector store (`entity_store`) where entities are embedded vectors with a
  `linked_memory_ids: [memory_id...]` array payload. **There are no edges at all.**
  `[verified:src=mem0/configs/base.py:29-58; mem0/memory/main.py:534-625]`
- **Legacy storage (v2, pre-pivot):** Real binary-edge property graph via Neo4j (`MemoryGraph`
  using `langchain_neo4j.Neo4jGraph`). Edges typed by the LLM-generated relationship string;
  edge properties `created_at`, `updated_at`, `mentions` (counter), `valid` (bool),
  `invalidated_at`. The relationship schema (`RELATIONS_TOOL`) enforced exactly
  `{source, relationship, destination}` — three string fields, binary only.
  `[verified:src=gh:graph_memory.py; gh:graphs/tools.py]`
- **What v3 did well:** Stable uuid4 handles, append-only SQLite `history` table (ADD/UPDATE/DELETE
  with old+new text), provenance (`actor_id`, `role`, `user_id`/`agent_id`/`run_id`), pluggable
  28-backend vector stores, hybrid scoring (semantic + BM25 + entity_boost, fused additively).
  `[verified:src=mem0/memory/storage.py; mem0/utils/scoring.py]`
- **Dead ends (where the representation costs mem0):**
  1. **N-ary facts unrepresentable** — the legacy `RELATIONS_TOOL` schema was binary-only;
     current v3 has no relations at all. Co-authorship forced into n binary edges.
  2. **No reification** — no handle for a fact-as-a-node; memories only point at entities,
     never at other memories.
  3. **Stringly-typed, LLM-generated relationship labels** — no type discipline; the same
     relation surfaces as `:professor`, `:teaches`, `:teacher_of` across runs.
  4. **The v3 "graph" is entity tagging with a cosine boost** — `combined = (semantic + bm25 +
     entity_boost)/max_possible`. No edge traversal, no multi-hop, no relationship type.
- **Verdict (the most consequential finding in this survey):** mem0 **built a binary graph
  layer and then deleted it** rather than maintain it. The stated reason was the maintenance/
  reliability cost of the LLM-driven extraction+update loop (two extra LLM calls per UPDATE/
  DELETE for conflict resolution). This is strong evidence that the binary graph's
  expressiveness **was not paying off** for the dominant (per-user preference facts) workload.
  mem0's trajectory is the clearest possible answer to "is binary good enough": for the mass
  market, **yes — and they retreated to pure vectors because even the binary graph was
  overkill**. But for agents that *need* relational structure, mem0's retreat leaves the niche
  open; they did not solve the relational problem, they abandoned it.

### 3.3 Letta (formerly MemGPT, letta-ai/letta) — the non-graph incumbent

- **Status:** Apache-2.0, ~24k★, v0.16.8, 167 Alembic migrations (heavily-evolved schema),
  Postgres (pgvector) production DB.
  `[verified:src]`
- **The headline finding:** **Letta has no graph layer.** Exhaustive grep for
  `knowledge_graph`, `class.*Graph`, `hyperedge`, `edge_type`, `entity_relation`,
  `relational_memory`, `memory_graph` returned **zero hits** across all `.py` files, the
  README, and the `fern/` docs tree. The premise "Letta is adding graph features recently" is
  not borne out by the source. `[verified:src]`
- **Storage — three tiers, none a graph:**
  1. **Core memory = editable `Block`s** (in-context, char-limited). `Memory = List[Block] +
     List[FileBlock]`. A `Block` is `{id, value:str, limit, label, metadata, tags}` — flat
     text, no structure inside. Mutated via `core_memory_append` / `core_memory_replace` (the
     latter is **plain exact-match string replace**, then overwrite-in-place).
     `[verified:src=letta/schemas/memory.py:68-80, 820-837]`
  2. **Recall memory = the message log.** `Message` references agents/conversations/runs but
     **never references other messages** — no parent/child, no reply-to, no edge.
     `[verified:src=letta/schemas/message.py]`
  3. **Archival memory = vector store of `Passage`s.** Embedding is a single pgvector column on
     the same row as the text — **unified column-wise with the symbolic record**. Retrieval is
     pure cosine similarity with optional tag/date filters. Turbopuffer dual-write adds
     ANN/BM25/hybrid with RRF. `[verified:src=letta/orm/passage.py:34-40; letta/helpers/tpuf_client.py]`
- **Identity:** Stripe-style prefixed IDs (`agent-<uuid8>`, `message-…`, `block-…`,
  `passage-…`, `identity-…`) from `LettaBase.generate_id`. Never reused, no ID-versioning.
  `[verified:src=letta/schemas/letta_base.py:32-47]`
- **The `Identity` construct (closest to a typed node):** An `Identity` is
  `{identifier_key, name, identity_type ∈ {org,user,other}, properties: List[IdentityProperty]}`
  where each `IdentityProperty = {key, value, type ∈ {string,number,boolean,json}}`. This is a
  **typed-property node** — but it links only to agents/blocks via many-to-many junction
  tables, and **there is no Identity↔Identity edge table** anywhere. So Identity is a node
  without edges. You cannot say "identity A works_for identity B."
  `[verified:src=letta/schemas/identity.py; letta/orm/identity.py]`
- **Tiered memory (the flagship strength):** Core (always-in-context, char-limited) ↔ Recall
  (paginated/summarized) ↔ Archival (unbounded, vector-retrieved), managed by a **sleep-time
  background agent** and a **partial-eviction Summarizer**. This is an OS-style paging metaphor
  (main memory ↔ disk) — the original MemGPT thesis.
  `[verified:src=letta/schemas/agent.py:318; letta/services/summarizer/summarizer.py:136-208]`
- **Dead ends (where the representation costs Letta):**
  1. **No relational structure between facts** — cannot represent "memory A contradicts memory
     B." Every passage is a flat independent row; contradiction detection is left to the LLM.
  2. **Core memory is opaque free-text to the system** — `Block.value` is a single `str`;
     structured facts live as prose only the LLM parses. Cross-fact reasoning must be done by
     the LLM over retrieved text.
  3. **`core_memory_replace` is brittle exact-match string editing** — updating one field of
     one fact requires the model to reproduce surrounding text perfectly.
  4. **Single-hop vector retrieval only** — no multi-hop traversal.
  5. **Versioning gap** — only `Block`s have history (`block_history` table with sequence
     numbers); passages and messages are overwrite-in-place with no audit trail.
     `[verified:src=letta/orm/block_history.py; letta/services/passage_manager.py:652-719]`
- **Verdict:** Letta's absence of graph structure is a **deliberate philosophy**, not an
  oversight: the LLM is the reasoning engine, memory is the legible text it reads/edits, and a
  typed graph would be dead weight the model must translate to/from. For chat/companion agents,
  free-text prose the LLM can summarize and rewrite is arguably more useful than a graph. The
  tiered-memory paging problem (their focus) is one a hypergraph does **not** solve — it's
  orthogonal. **The hypergraph is not redundant with Letta; it addresses a different failure
  mode (relational consistency) that Letta explicitly offloads to the LLM.** The opening is for
  agents that *must* maintain a consistent belief graph, reason over multi-step relationships,
  or track evidence provenance across facts.

### 3.4 Cognee (topoteretes/cognee) — genuinely graph, strictly binary

- **Status:** Apache-2.0, ~29k★, last commit 2026-07-21, production-leaning (Alembic-managed,
  multi-backend, MCP server, frontend, eval framework). `[verified:web]`
- **Storage — three stores, kept in sync by the ingestion pipeline:** relational (bookkeeping),
  graph (symbolic), vector (embeddings). `[verified:src]`
- **Backends:** Graph — Neo4j, FalkorDB, Neptune, Kuzu, Turso, Postgres/Apache-AGE, "Ladybug"
  (7 graph backends). Vector — LanceDB, pgvector, Turso.
  `[verified:src=cognee/infrastructure/databases/{graph,vector}]`
- **Edge model — strictly binary at every layer:**
  1. The extraction prompt mandates `(start_node, relationship_name, end_node)`.
     `[verified:src=tasks/graph/cascade_extract/prompts/extract_graph_edge_triplets_prompt_system.txt]`
  2. The `Triplet` model has `from_node_id`/`to_node_id`.
     `[verified:src=cognee/modules/engine/models/Triplet.py]`
  3. The in-memory `Edge` (`CogneeGraphElements`) is `node1`/`node2` — `__hash__` uses
     `(node1, node2)`; `__eq__` compares exactly two endpoints; `to_json()` emits
     `source_node_id`/`target_node_id`. No list-of-endpoints, no hyperedge field.
     `[verified:src=cognee/modules/graph/cognee_graph/CogneeGraphElements.py]`
  4. The relational `edges` table has exactly `source_node_id UUID NOT NULL` and
     `destination_node_id UUID NOT NULL`, `relationship_name Text`, `attributes JSON`. **Two
     endpoint columns, no more.** `[verified:src=alembic/versions/84e5d08260d6...]`
  5. The Neo4j adapter persists edges as `MERGE (from_node)-[r:TYPE]->(to_node)` — the
     canonical property-graph binary edge. `[verified:src=infrastructure/databases/graph/neo4j_driver/adapter.py]`
- **Identity — content-addressed (the best of the four):** `DataPoint.id_for(*values)` computes
  `uuid5(NAMESPACE_OID, f"{ClassName}:{joined}")` — deterministic, stable across runs,
  re-ingestion merges rather than duplicates. `[verified:src=cognee/infrastructure/engine/models/DataPoint.py]`
- **Provenance — real and operational:** Per-DataPoint fields `source_pipeline`,
  `source_task`, `source_user`, `source_content_hash`, `dataset_id`, `data_id`,
  `pipeline_run_id`. The `GraphVectorStoreInterface` exposes `delete_by_source_ref`,
  `delete_by_dataset_id`, `rollback_by_pipeline_run_id` — provenance is **operationally usable
  for delete/rollback**, not decorative. `[verified:src=alembic/versions/aa753a730673...; infrastructure/databases/unified/graph_vector_store_interface.py]`
- **Learnable edges — data slots exist, no learner:** `Edge.weight` and
  `Edge.weights: dict[str,float]` (e.g. `confidence`, `strength`, `importance`) plus
  `DataPoint.feedback_weight` and `importance_weight` provide the *storage* for learnable
  edges; a `memify` pipeline writes feedback back. But there is **no online learner / RL update
  rule** — weights are stamped, not learned by gradient/reward.
  `[verified:src=cognee/infrastructure/engine/models/{Edge,DataPoint}.py; memify_pipelines/persist_agent_trace_feedbacks_in_knowledge_graph.py]`
- **Retrieval is vector-first despite the graph:** The `TripletRetriever` performs pure vector
  similarity on a `"Triplet_text"` embedding collection and concatenates top-k triplet texts —
  **no graph traversal** in this path. The "unified" interface only unifies delete/rollback
  lifecycle, not query. `[verified:src=cognee/modules/retrieval/triplet_retriever.py]`
- **Dead ends (where binary costs Cognee):**
  1. **N-ary facts fragmented** — extraction prompt forces binary; co-authorship becomes a
     star/clique of 2–3 edges with no shared handle. The single "co-authored" relationship
     loses group-level properties (venue, date) and cell-level participation (role per author).
  2. **No edge reification at the structural level** — `belongs_to_set`/`NodeSet` gives node
     grouping, but an edge cannot be a first-class endpoint of another edge.
  3. **Property flattening is lossy in Neo4j** — `Edge.weights` exploded into `weight_<name>`
     props, other dicts JSON-stringified (`_json` suffix). A direct recurring tax of the binary
     property-graph substrate.
  4. **No mutation history** — `version` counter + `updated_at` exist but overwrite-in-place
     (`ON MATCH SET updated_at = timestamp()`). Last-write-wins, not versioning.
- **Verdict:** Cognee is the cleanest confirmation that the field treats "knowledge graph" as
  synonymous with **binary property graph**. The symbolic structure is genuinely load-bearing
  (storage, scoring, visualization), but it is binary at every layer, and the limits are real
  and felt. The reason is visible in Cognee's own architecture: the moment you commit to
  multiple off-the-shelf backends (Neo4j/FalkorDB/Kuzu/Neptune/pgvector), you are locked into
  the binary property-graph contract those backends implement. **Building (and finding backends
  for) a typed-property hypergraph is the actual unsolved problem.**

---

## 4. The comparison matrix — AI-memory capabilities across the four competitors

Same 9 axes as `BASE_INVESTIGATION.md §4` (with N-ary support swapped in for the
hypergraph-specific column). ✓ = present · partial = partial · ✗ = absent.

| Capability | Graphiti | mem0 (v3) | mem0 (legacy v2) | Letta | Cognee |
|---|---|---|---|---|---|
| **Reification** (edge-as-member) | ✗ | ✗ | ✗ | ✗ | partial (node grouping only) |
| **Embeddings unified w/ symbolic** | **✓** | partial (parallel store) | ✓ (on node) | ✓ (column on row) | ✗ (parallel stores) |
| **Learnable/updatable edges** | ✗ | ✗ | partial (mentions counter) | ✗ | partial (data slots, no learner) |
| **Provenance** | **✓** (episode list) | **✓** (actor/role/run) | ✓ | partial (file/archive only) | **✓** (rollback-by-run) |
| **Versioning** | partial (via intervals) | **✓** (history table) | partial | partial (blocks only) | ✗ (last-write-wins) |
| **Tiered memory** | partial (structural) | ✗ | ✗ | **✓** (core/recall/archival) | partial (orchestration) |
| **Typed properties** | partial (Pydantic dev-time) | ✗ | ✗ | partial (Identity) | ✗ (stringly dicts) |
| **Stable handles** | partial (uuid4, not content-addressed) | ✓ (uuid4) | ✓ | ✓ (prefixed uuid) | **✓** (content-addressed uuid5) |
| **N-ary relationship support** | ✗ | ✗ | ✗ | ✗ | ✗ |

**The pattern:** Every system scores ✗ on **N-ary relationship support** — the one capability
that is hypergraph-native. Every system scores ✗ or partial on **Reification** and **Learnable
edges** — two of the five capabilities the base investigation identified as AI-memory-critical.
No system combines ≥7 of the 9; the strongest (Graphiti, Cognee) top out around 4–5 with
several "partial" caveats. This is the same shape as the library matrix in `BASE_INVESTIGATION
§4`: **the AI-native combination (reification + typed properties + stable handles + n-ary +
learnable edges) appears in zero systems.**

---

## 5. The central question, answered

> **Is the hypergraph gap unfilled because nobody built it, or because the field tried
> hypergraphs and decided binary was good enough?**

### 5.1 The field did NOT try hypergraphs and reject them

**Zero evidence** in any of the four codebases of hypergraph modeling ever being considered,
prototyped, or discussed:

- **Graphiti:** `gh search issues` for `hypergraph|hyperedge|n-ary` returned **0 results**.
  The binary choice is enforced at the extraction-prompt layer ("Each fact must involve two
  distinct entities") before any modeling question is even asked.
  `[verified:src=gh search issues]`
- **mem0:** Full-repo grep for `hypergraph|n-ary|reify edge|ternary relation` returned **zero
  matches** across codebase, docs, and issues. The binary triple was assumed from the start
  (it mirrors how LLMs emit facts). `[verified:src=grep]`
- **Letta:** Grep for graph constructs returned **zero hits**; Letta was never a graph system.
  `[verified:src]`
- **Cognee:** Full recursive tree search for `hyperedge|nary|hypergraph|reified` filenames
  returned **zero hits**. No ADR, design doc, or comment discusses or rejects hypergraphs.
  `[verified:src=gh git/trees]`

**Conclusion:** The binary-graph (or non-graph) choice is an **unexamined default inherited
from the property-graph DB lineage**, not a considered-and-rejected decision. The hypothesis
"the field tried hypergraphs and decided binary was good enough" is **falsified** — the field
never tried.

### 5.2 Binary IS good enough — for the dominant 80% workload

For **per-user, pairwise, single-fact-at-a-time conversational memory** (preferences,
attributes, "user likes tennis," "user lives in city"), binary graphs and pure vector stores
are genuinely sufficient. All four systems serve this workload well and are commercially
successful at it. mem0's April-2026 retreat from graphs to pure vectors is the sharpest
possible evidence: for the mass market, **even the binary graph was overkill** — they traded
relational expressiveness for simpler/faster/more-reliable vector retrieval and won.

Graphiti's temporal-interval model + episode provenance is a genuinely strong design for the
pairwise-temporal core, and it does not need hyperedges. Neo4j/FalkorDB's engineering maturity
(sharded, operationally proven, sub-200ms at scale) is a decisive practical advantage no
hypergraph DB matches today.

### 5.3 But binary actively costs expressiveness in three specific places — and that is the opening

The same three failure modes recur across all four systems. They are exactly the capabilities
Topos proposes:

1. **N-ary facts are structurally unrepresentable.** In Graphiti this is *enforced* by the
   extraction prompt ("Each fact must involve two distinct entities"); in Cognee by the
   `(start_node, relationship_name, end_node)` prompt + binary `edges` table; in mem0 by the
   `RELATIONS_TOOL` schema; in Letta by the absence of relations. A fact like "Alice, Bob, and
   Carol co-authored paper X" cannot exist as one memory — it is lossily decomposed into a
   clique or star, losing the joint event and the ability to ask "who co-authored X together"
   atomically. `[verified across all four]`

2. **Cell-level / per-participation properties don't exist.** In "Alice AUTHORED Paper (as
   corresponding author) and Bob AUTHORED Paper (as first author)," the role differs per
   participant. Every system has only node-level and edge-level property bags — per-membership
   metadata has no home and gets jammed into free text or dropped. HyperNetX's MultiIndex
   incidence properties (the cell-properties primitive in Topos's contract) solve this directly.
   `[verified: BASE_INVESTIGATION §3.1, §5.5 #14]`

3. **Reified facts-as-entities (memory-of-memory) are impossible at the model layer.** You
   cannot link *to* a fact. Graphiti approximates it via `invalid_at`/`expired_at` + a new edge;
   Letta cannot mark a superseded fact at all. The one place reification appears (Graphiti's
   Kuzu `RelatesToNode_` trick) is a storage workaround forced by a DB limitation, not a usable
   abstraction. RDF 1.2 §1.5 standardizes exactly this (triple terms as a fourth RDF type).
   `[verified: BASE_INVESTIGATION §3.7]`

A fourth recurring cost — **no persisted learnable edge weights** — appears in all four
(ranking is recomputed at query time, then discarded). Cognee is the closest, with
`Edge.weights: dict[str,float]` data slots, but no online learner updates them. A
reinforcement-style memory needs edges that *carry* and *update* confidence.

### 5.4 Why is the gap unfilled, if it's real?

Three reasons, in order of weight:

1. **No n-ary DB satisfies the intersection of properties the agent-memory field selected for.**
   This is the deepest reason and it is visible in the incumbents' own architecture choices.
   Graphiti deprecated Kuzu (a mere *property-graph* DB) for being unmaintained; Cognee supports
   7 graph backends precisely because they all implement the same binary property-graph contract.
   But this is **not** the broad claim "no production-grade n-ary DB exists" — that would be
   false (TypeDB is production-grade and genuinely n-ary; see §5.5). The precise, defensible
   claim: **n-ary DBs exist, but none satisfies the intersection the agent-memory field
   selected for — *embedded or server-light* + *Cypher/property-graph query language* +
   *interchangeable backends* + *permissive license* + *large LLM-tooling ecosystem*.** Only
   binary-edge DBs (Neo4j/FalkorDB/Kuzu) satisfy that intersection. TypeDB (the real n-ary
   option) is server-only, TypeQL-locked, and ecosystem-small; TigerGraph (a skeptic's other
   favorite) is *not actually n-ary* — its GSQL `CREATE EDGE` grammar enforces exactly two
   endpoints. `[verified:docs=tigergraph.com/docs/gsql-ref]` **The hypergraph gap is unfilled
   not because n-ary DBs are technically impossible, but because the n-ary option doesn't fit
   the deployment/query-language/ecosystem shape agent-memory libs want — which is exactly the
   shape Topos (embedded, C#, permissive) proposes to fill.** `[unverified:inferred]` from the
   architecture + DB evidence; see §5.5 for the full DB-capability matrix.

2. **The LLM-extraction framing pre-commits to binary.** "Extract fact triples" is the default
   prompt shape in every graph system (Graphiti, Cognee, mem0-legacy). This mirrors how LLMs
   naturally emit facts (subject-predicate-object) and locks in binary *before* any modeling
   question is asked. `[verified:src=prompts across the three graph systems]`

3. **The dominant workload hasn't yet forced it.** The incumbents are successful serving
   pairwise-temporal conversational memory. The cases that would force hypergraph modeling —
   agents maintaining consistent belief graphs, multi-step relational reasoning, evidence
   provenance across facts — are emerging but not yet the mass market. mem0's retreat shows the
   field optimizing hard for the 80%, leaving the relational 20% open.

### 5.5 Preempting the obvious attacks (the questions a sharp reviewer will ask)

A skeptic attacking this survey will name specific systems and DBs. This section exists so the
thesis survives those attacks rather than being blindsided by them.

**Attack A — "But TypeDB/TigerGraph support n-ary relationships!"**
The skeptic is **1.5/3 right**, and the half that's right (TypeDB) doesn't break the thesis.

| DB | N-ary capability | Why the competitors didn't use it |
|---|---|---|
| **TypeDB** (vaticle) | **NATIVE n-ary** — role-based relations, variadic roles, native reification. Production-grade (3.x, 2024). `[verified:docs=typedb.com]` | **Server-only** (no embedded mode despite community demand); **TypeQL** is a proprietary query language with small ecosystem and no text-to-X LLM tooling; license is **MPL-2.0** (permissive, not a blocker — *correction:* `BASE_INVESTIGATION.md` §3.9 wrongly says "GPL-3"). Fails the *embedded + Cypher + ecosystem* intersection the market selected. |
| **TigerGraph** | **BINARY ONLY** (skeptic is wrong). `CREATE EDGE` grammar: `FROM V, TO V [\| FROM V, TO V]*` — exactly two endpoints per instance. `[verified:docs=tigergraph.com/docs/gsql-ref]` | Plus it's **proprietary** (Community Edition ≤50GB; Enterprise needs commercial license) — a real blocker for OSS libs. |
| **Grakn** | NATIVE n-ary — but it's just **TypeDB's former name** (Grakn → TypeDB 2.0). Same entity. `[verified:web]` | Same as TypeDB. |

**The net:** the only genuinely-n-ary production DB (TypeDB) fails the deployment shape the
field selected for; the other named examples are either binary (TigerGraph) or a rename (Grakn).
**This is the precise, defensible form of the "why binary won" claim** — not the broad
overstatement "no n-ary DB exists," which TypeDB refutes.

**Attack B — "But real hypergraph memory systems exist (HyperGraphRAG, HGMEM, HyperMem)!"**
They exist, but **none is a production stateful agent-memory substrate** — they are research
prototypes in adjacent niches. Naming them preempts the attack and demonstrates thoroughness.

| System | What it is | Why it doesn't fill the substrate gap |
|---|---|---|
| **HyperGraphRAG** (Luo et al., NeurIPS 2025; LHRLAB/HyperGraphRAG, 431★) | Document-RAG over a static knowledge hypergraph. True n-ary hyperedges. `[verified:web=arxiv.org/abs/2503.21322]` | Stateless retriever over a static corpus, not evolving agent memory. Same category as GraphRAG/LightRAG — correctly out of substrate scope. |
| **HGMEM** (arXiv 2512.23959; Encyclomen/HGMem, 131★) | Hypergraph *working* memory for multi-step RAG; hyperedges = memory units. `[verified:web=arxiv.org/abs/2512.23959]` | Working memory scoped to a *single reasoning pass* over documents; no persistent cross-session state. Research benchmark. |
| **HyperMem** (Yue et al., ACL 2026; EverMind-AI/HyperMem, 12★) | Hypergraph memory for long-term conversations; groups episodes/facts via hyperedges. `[verified:web=arxiv.org/abs/2604.08256]` | **The closest to the thesis.** But it's a 2026 research benchmark with ~12★ and no framework adoption, and it uses hyperedges for *topic clustering* of memories — not n-ary relational facts as first-class memory primitives. A reviewer could argue it partially fills the gap; the defense is "research prototype vs. production substrate." |

**The pattern:** the hypergraph idea is *alive in research* (3 real systems, all 2025-2026), but
**unported to a persistent, evolving, production agent-memory substrate.** That is exactly the
niche Topos targets. Note also: an independent survey (`DEEP-PolyU/Awesome-GraphMemory`,
arXiv 2602.05665) lists HyperGraphRAG as the *only* hypergraph-related system in the agent-memory
landscape — corroborating "the gap is unfilled" from a second source.
`[verified:web=github.com/DEEP-PolyU/Awesome-GraphMemory]`

**Attack C — "Did you miss other agent-memory competitors?"**
The four surveyed are the production stateful-memory substrates. Other named systems are either
out of scope or non-graph peers:
- **A-MEM** (NeurIPS 2025, `agiresearch/a-mem`) — a real *stateful non-graph* peer: self-organizing
  memory evolution via ChromaDB vectors + similarity-linked notes. Reinforces the "the niche is
  binary-graph OR non-graph" point; worth naming.
  `[verified:web=github.com/agiresearch/a-mem]`
- **GraphRAG / LightRAG / HyperGraphRAG / Hyper-RAG** — document-RAG (stateless retrievers over
  static corpora), not evolving agent memory. The stateful/stateless distinction is the scope
  line. `[verified:web=letta.com/blog/rag-vs-agent-memory/]`
- **LangMem / LlamaIndex Memory / Semantic Kernel memory** — framework memory modules (SDKs of
  primitives storing JSON), not standalone substrates; mem0 integrates *into* them as the backing
  store. `[verified:web]`
- **Neo4j / FalkorDB / Kuzu / Memgraph / TigerGraph / Weaviate / Qdrant / Pinecone / Chroma** —
  backends (DBs), not memory systems. Correctly excluded.

No missed production stateful substrate that threatens the thesis. The hypergraph gap is real and
open.

---

## 6. The empirical counter-argument (Fable's point, carried forward)

The library survey (`BASE_INVESTIGATION.md`) and this competitor survey together establish that
**the hypergraph gap is real and unfilled because nobody built it — not because the field
rejected it.** But neither survey can prove *binary is insufficient*; that requires the
empirical argument from a workload that actually needs n-ary composition.

That argument lives in Rich-Learning-Base's own evidence and should open the final spec (per
Fable — it's the part a skeptic attacks, and no library survey answers it):

> **RLB's paradox-compression finding + the deferred-HyperEdge trigger:** n-ary composition
> with measured non-derivable payloads cannot be faithfully expressed in binary edges without
> lossy encoding. RLB's 337-test V2 suite hit this directly — the deferred-HyperEdge trigger
> exists because the binary representation could not carry the joint payload without loss.

This is the empirical proof that binary is *not* always good enough — and it comes from the
first consumer Topos is being built for (per `DECISIONS.md §6`: build as RLB's kernel first).
**The competitor survey establishes the gap is open; RLB's evidence establishes it is real.
Together they answer the central question.**

---

## 7. What this changes for Topos

1. **The feasibility question is answered at the survey level.** The hypergraph gap is unfilled
   because nobody built it (§5.1), binary is good enough for the 80% (§5.2) but actively costs
   expressiveness in the relational 20% (§5.3), and the reason is the absence of a mature
   hypergraph backend — which is what Topos is. The spec can open with this conclusion.

2. **The spec's §1 should open with the RLB empirical argument** (paradox-compression +
   deferred-HyperEdge), not with the library survey. The survey establishes the gap is open;
   RLB establishes it is real. (Fable's point, confirmed.)

3. **Three specific Topos primitives map directly onto the three recurring failure modes** the
   survey identified — strong validation of the 4-primitive + 2-invariant contract:
   - **N-ary Incidence primitive** → solves the n-ary-fact fragmentation (§5.3 #1).
   - **Cell-level properties on Incidence** → solves the per-participation-property gap (§5.3
     #2). HyperNetX's MultiIndex is the provenance.
   - **Reification via `Role:Edge` vertex** → solves memory-of-memory (§5.3 #3). RDF 1.2 §1.5
     and TypeDB validate the pattern.
   - **`Edge.weights`-equivalent on Incidence** → the persisted learnable-weight slot Cognee
     half-built but never connected to a learner. (Cognee's `Edge.weights: dict[str,float]` is
     partial validation that the storage slot is wanted.)

4. **The competitors validate two of Topos's design choices by independent arrival:**
   - **Content-addressed identity** (Cognee's `uuid5(ClassName:joined)`) is the strongest
     identity model among the incumbents and matches Topos's "stable handles" + newtype
     contract — better than Graphiti's non-deterministic uuid4.
   - **Unified embeddings on the symbolic record** (Graphiti's `fact_embedding` on edges;
     Letta's pgvector column on the passage row) validate the decision (in `DECISIONS.md §1`
     Q5) to make embeddings a `PropertyKey<float[]>` rather than a parallel store. Graphiti and
     Letta independently arrived at unification; mem0 and Cognee's *parallel* stores are the
     counter-example (harder to keep in sync).

5. **A direct warning from mem0:** building a graph layer that the LLM must populate via
   extraction is operationally expensive (mem0 deleted theirs over the LLM-call maintenance
   cost). Topos is a *library/substrate*, not an extraction pipeline — but the consumer
   (RLB/M5 chat demo) must not depend on fragile LLM-driven extraction as the *only* write
   path. The spec should note this as a consumer-design risk.

6. **The Topos intersection is defensible but specific.** §5.5 reframes "why binary won" from
   the (false) broad claim to the precise intersection: *embedded + Cypher + permissive license
   + ecosystem*. Topos is **embedded, C#, permissive** — it satisfies the deployment shape the
   field selected for, but with n-ary primitives instead of binary. The honest implication:
   Topos's defensibility rests on being *the first n-ary substrate at that intersection*, not
   on n-ary being impossible in general (TypeDB proves it's possible server-side). The spec
   should state this precisely. Note also: the C# niche is itself unoccupied — none of the
   four competitors, nor TypeDB/TigerGraph, ships a C# memory substrate, which is the gap
   `BASE_INVESTIGATION.md` established independently.

---

## 8. Honest caveats

- **Source-grade, not formally-verified.** "Is binary good enough" is a judgment, not a
  decidable property. The judgment here is grounded in source-verified structural limits
  (enforced-binary prompts, two-column edge tables, absent cell properties) — but it remains
  the spec writers' call whether those limits justify a new library.
- **mem0's pivot is recent (2026-04-14).** The OSS v3 architecture may evolve; the platform's
  proprietary "native graph" may gain declared relationships. Re-check before citing mem0's
  "no graph" status as a stable fact. `[verified:src=gh:commits/a488e19044e4]`
- **Letta's "no graph" finding is strong but dated to v0.16.8 (HEAD 2026-07-03).** A graph
  layer could land later. The design-philosophy argument (LLM-as-reasoner, text-as-memory)
  is the durable finding; the codebase absence is point-in-time.
- **Cognee's retrieval path is vector-first** despite the graph being load-bearing for storage.
  The claim "Cognee is genuinely graph-structured" is true at the storage/scoring layer but
  weaker at the retrieval layer (`TripletRetriever` does no traversal). Don't overstate Cognee
  as a graph-retrieval system.
- **The hypergraph-DB-immaturity argument (§5.4 #1) has been narrowed from its original broad
  form.** An earlier draft said "no production-grade hypergraph/n-ary DB exists at Neo4j's
  maturity" — that is **false** (TypeDB is production-grade and genuinely n-ary) and was
  corrected to the intersection form: no n-ary DB satisfies *embedded + Cypher + permissive +
  ecosystem* simultaneously. The correction surfaced one factual error in
  `BASE_INVESTIGATION.md` §3.9 (TypeDB license "GPL-3" → actually MPL-2.0), now fixed there.
  See §5.5 Attack A for the full DB-capability matrix. The narrower claim is defensible; the
  broad claim was not.

- **The "zero hypergraph evidence" greps (§5.1) are GitHub-code-search-index-based, not full
  clone-and-grep.** `gh search code` indexes the default branch and may miss terms in large
  files, binary blobs, or non-default branches. The negatives are consistent and the terms are
  distinctive, so this is strong evidence — but for airtight certainty, a recursive `git grep`
  on local clones would be the next step. The one place the search *would* have caught a real
  hypergraph claim is Graphiti's own paper (§3.1 ⚠ callout), which uses "hyper-edge" in prose
  — and that claim is refuted by the formal `N_s × N_s` definition in the same paper, so it
  does not weaken the thesis.

- **Three real hypergraph research prototypes exist** (HyperGraphRAG, HGMEM, HyperMem — §5.5
  Attack B) and are correctly out of substrate scope, but a reviewer will name them. The
  defense (research prototype vs. production substrate; document-RAG/working-memory vs.
  persistent stateful memory; topic-clustering vs. n-ary relational primitives) is in §5.5.
  HyperMem (ACL 2026, ~12★) is the closest to the thesis and the one to watch.
- **Nasser should read the Medium Kuzu piece directly** before M4 (`docs/SESSION_HANDOFF.md §7`)
  — it's the deepest public architectural analysis of Kuzu's storage engine, relevant to the
  persistence tier Topos will build.

---

## 9. Sources (for the spec writers' audit)

**Graphiti (getzep/graphiti):**
- `graphiti_core/nodes.py` — Node/EntityNode/EpisodicNode/CommunityNode/SagaNode, uuid4 identity
- `graphiti_core/edges.py` — binary `Edge` ABC, `EntityEdge` temporal fields
- `graphiti_core/driver/driver.py` — `GraphProvider` enum (Kuzu deprecated)
- `graphiti_core/models/edges/edge_db_queries.py` — Kuzu `RelatesToNode_` reification
- `graphiti_core/prompts/extract_edges.py` — RULE 2 forbidding n-ary
- `graphiti_core/graphiti_types.py`, `search/search_config_recipes.py` — query-time rerankers
- `gh search issues` for hypergraph/hyperedge/n-ary (0 results)
- arXiv 2501.13956 ("Zep: A Temporal Knowledge Graph Architecture for Agent Memory")

**mem0 (mem0ai/mem0):**
- `mem0/configs/base.py` — `MemoryConfig` (no `graph_store` in v3)
- `mem0/memory/main.py` — `Memory.__init__`, `add`, `search`, `update`, `entity_store`
- `mem0/memory/storage.py` — `SQLiteManager` append-only history
- `mem0/vector_stores/base.py` — `VectorStoreBase` interface
- `mem0/utils/scoring.py` — hybrid fusion (semantic + bm25 + entity_boost)
- Commit `a488e19044e4` "feat(oss): port v3 pipeline" (2026-04-14, graph layer deleted)
- `gh:graph_memory.py@57f944e18`, `gh:graphs/tools.py`, `gh:graphs/utils.py` (legacy v2 graph)
- `docs/platform/features/graph-memory.mdx` — "co-occurrence not declared"
- Full-repo grep for hypergraph/n-ary/reify (zero matches)

**Letta (letta-ai/letta):**
- `letta/schemas/memory.py` — `Memory` = `List[Block]`, `core_memory_replace` exact-match
- `letta/schemas/block.py` — `BaseBlock`/`Block` fields, char limit
- `letta/schemas/passage.py`, `letta/orm/passage.py` — `Passage`, pgvector column unified
- `letta/schemas/message.py` — `Message`, no inter-message refs
- `letta/schemas/identity.py`, `letta/orm/identity.py` — `Identity` typed node, no edges
- `letta/schemas/letta_base.py` — prefixed-ID generation
- `letta/orm/block_history.py` — block-only versioning
- `letta/services/summarizer/summarizer.py` — partial-eviction tier promotion
- `letta/helpers/tpuf_client.py` — Turbopuffer hybrid + RRF
- Exhaustive grep for graph/edge/knowledge_graph (zero hits)

**Cognee (topoteretes/cognee):**
- `cognee/infrastructure/engine/models/Edge.py`, `DataPoint.py` — persistence base models
- `cognee/modules/engine/models/Triplet.py`, `Entity.py`, `node_set.py` — graph models
- `cognee/modules/graph/cognee_graph/CogneeGraphElements.py` — binary in-memory `Edge`
- `cognee/infrastructure/databases/graph/neo4j_driver/adapter.py` — binary MERGE
- `cognee/infrastructure/databases/unified/graph_vector_store_interface.py` — lifecycle-only unification
- `cognee/modules/retrieval/triplet_retriever.py` — vector-first retrieval, no traversal
- `cognee/tasks/graph/cascade_extract/prompts/extract_graph_edge_triplets_prompt_system.txt` — binary prompt
- `cognee/alembic/versions/84e5d08260d6...` — binary `edges` table
- `cognee/alembic/versions/aa753a730673...` — `pipeline_run_id` provenance
- `cognee/memify_pipelines/persist_agent_trace_feedbacks_in_knowledge_graph.py` — learnable-edge slot
- Full recursive tree search for hyperedge/nary/hypergraph (zero hits)

**N-ary DB capability + research-prototype evidence (§5.5):**
- TypeDB n-ary: `typedb.com/blog/why-typedb-isnt-a-graph-database-but-it-can-behave-as-one`,
  `typedb.com/fundamentals/pera-model-guide` (variadic roles)
- TypeDB license MPL-2.0: `raw.githubusercontent.com/vaticle/typedb/master/LICENSE`
- TypeDB server-only (no embedded): `typedb.com/features`, `typedb.com/docs/core-concept/drivers/overview`
- TigerGraph binary-edge grammar: `tigergraph.com/docs/gsql-ref/4.2/ddl-and-loading/defining-a-graph-schema`
- Graphiti paper "hyper-edges" prose + `N_s × N_s` formal def: `arxiv.org/html/2501.13956v1`
- HyperGraphRAG: `arxiv.org/abs/2503.21322`, `github.com/LHRLAB/HyperGraphRAG` (NeurIPS 2025)
- HGMEM: `arxiv.org/abs/2512.23959`, `github.com/Encyclomen/HGMem`
- HyperMem: `arxiv.org/abs/2604.08256`, `github.com/EverMind-AI/HyperMem` (ACL 2026)
- A-MEM: `github.com/agiresearch/a-mem` (NeurIPS 2025, stateful non-graph peer)
- Independent survey: `arxiv.org/abs/2602.05665` + `github.com/DEEP-PolyU/Awesome-GraphMemory`
- Scope distinction (RAG vs agent memory): `letta.com/blog/rag-vs-agent-memory/`
