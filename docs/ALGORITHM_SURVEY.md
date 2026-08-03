# Topos — Fresh Independent Hypergraph Library Survey (algorithm completeness input)

**Date:** 2026-07-30 · **Author:** GLM-5.2 (ZCode, documentation role) · **Status:** Evidence base
for `docs/ALGORITHM_SPEC.md`. This is a **fresh independent** web-research pass (not a re-read of
`docs/BASE_INVESTIGATION.md`), commissioned to catch what the 5-day-old investigation may have
missed and to ground a must-have/good-to-have/no-need bucketing.

**Integrity standard:** source-verified facts carry `[verified:src=...]` / `[verified:web=...]`.
Items I could not verify from primary source are `[unverified]`. Star counts/versions retrieved
2026-07-29/30 via GitHub API, crates.io, PyPI, CRAN, NuGet, npm.

**Headline finding:** the fresh pass surfaced several libraries `BASE_INVESTIGATION.md` barely or
didn't cover — **xgi** (the most active Python lib), **hypergraphx/HGX** (community/motif leader),
**TopoNetX** (TDL structure breadth), **SimpleDirectedHypergraphs.jl** (directed Julia), **mhgl**
(Rust, simplicial operators), **open-hypergraphs** (Rust, categorical/DL). It independently
re-verified the existing doc's claims (all hold) and gave a clear verdict on the **oracle question**
that's the crux for hypergraph-native algorithm verification.

---

## 1. The library universe (expanded beyond the base investigation)

| # | Library | Language | Category | Status | In base investigation? |
|---|---|---|---|---|---|
| 1 | **xgi** (`xgi-org/xgi`) | Python | General-purpose, stats framework | Active, 254★, v0.10.2 | **NO — major miss** |
| 2 | HyperNetX (PNNL) | Python | s-centrality, concept lattices | Active, 706★, v2.4.3 | yes |
| 3 | **hypergraphx/HGX** | Python | Community detection, motifs, temporal/multiplex | Active, 330★, v1.8.0 | **NO — major miss** |
| 4 | DHG/DeepHypergraph | Python/PyTorch | GNN/HGNN framework | Alive-slow, 874★, v0.9.5 | yes |
| 5 | **TopoNetX** | Python | Topological DL, structure breadth | Very active, 274★, v0.4.0 | **NO — miss** |
| 6 | yamafaktory/hypergraph | Rust | General-purpose, directed, trait-based | Active, 351★, v4.2.0 | yes |
| 7 | **mhgl** | Rust | Simplicial operators | Tiny, 9★, v0.2.4 | NO |
| 8 | **open-hypergraphs** | Rust | Categorical DL / string diagrams | Active, 24★, v0.3.2 | NO |
| 9 | SimpleHypergraphs.jl | Julia | s-walk, metadata slots | Moderate, 86★, v0.3.4 | yes |
| 10 | **SimpleDirectedHypergraphs.jl** | Julia | Directed companion | Brand-new, 3★, v0.2.0 | **NO — miss** |
| 11 | KaHyPar / Mt-KaHyPar | C++ | Partitioning (storage patterns) | Active, 529★/189★ | yes |
| 12 | **HyperGraphLib** (alex-87) | C++ | General-purpose (adjacency matrix) | Tiny, 26★ | NO |
| 13 | JGraphT | Java | Binary graph algorithms (NO hypergraph) | Active, 2,777★ | yes (claim re-verified) |
| 14 | **Go: HypergraphGo** | Go | Pivoted to HoTT kernel | 5★ | NO |
| 15 | **C#: MarkupAxis.HyperMap** | C# | "Hypergraph" (unverifiable, dead source) | 790 dl, repo 404s | NO |
| 16 | **R: HyperG, rhype** | R | Spectral algebra / early-stage | Frozen / dormant | NO |
| 17 | JS/TS: d3-hypergraph, tscircuit | JS/TS | Viz / PCB routing (not math hypergraphs) | Various | NO |

**Three notable misses in the base investigation, now filled:**

1. **xgi** is the most active and best-engineered general-purpose Python hypergraph library, with a
   distinctive **`xgi.stats` framework** — lazy, composable per-node/per-edge statistic views
   (`H.nodes.<stat>` with `argmin/argmax/filterby`). This is the cleanest "typed-property access
   over a graph" API in the space and directly relevant to Topos's typed-property design.
   `[verified:src=xgi-org/xgi/xgi/stats/]`
2. **hypergraphx (HGX)** is the only library with **temporal + multiplex + signed** hypergraphs as
   first-class types, plus the richest community-detection suite (Hy-MMSBM, Hypergraph-MT, hyperlink
   communities) and **motif analysis**. `[verified:src=HGX-Team/hypergraphx/core/, communities/]`
3. **TopoNetX** offers the broadest structure-type taxonomy (simplicial/cell/combinatorial/
   colored-hypergraph/path-complex) for topological deep learning.

---

## 2. Per-library detail (the fresh facts)

### Python

**xgi** (`xgi-org/xgi`, v0.10.2, 254★, last push 2026-07-26, BSD-3-Clause per PyPI)
- **Storage:** NetworkX-inspired incidence dict-of-sets (`IDDict` node→edge-ids, edge→node-ids, plus
  per-node/edge/net attr dicts). Sparse matrices are *views* produced on demand by the `linalg`
  module, not the backing store. Multiedges allowed; any hashable as node.
  `[verified:src=xgi/core/hypergraph.py]`
- **Feature surface:** Hypergraph + **DiHypergraph** + SimplicialComplex as first-class peers; BFS,
  shortest path, connected components; **richest centrality suite in the survey** (h-/z-eigenvector,
  uniform-h, katz, clique-eigenvector, line-vector, node-edge); spectral (incidence/adjacency/
  laplacian/multiorder-laplacian/**adjacency_tensor**); s-walk exposed via `neighbors(s=)` +
  `adjacency_matrix(s=)`; clustering coefficient; assortativity; **spectral clustering** (no
  Louvain/modularity); simpliciality measures; generators; I/O including **HIF**; Kuramoto dynamics.
- **Distinctive:** the `xgi.stats` framework (lazy stat views) — most relevant to Topos's typed-
  property goal. Directed hypergraphs as first-class peer.

**HyperNetX/HNX** (`pnnl/HyperNetX`, v2.4.3, 706★, last push 2026-07-08, BSD-3-Clause)
- **Storage:** pandas DataFrames — `IncidenceStore` (2-col edge/node pairs + derived dicts) +
  `PropertyStore` (per-uid props + **per-incidence/cell-level props via MultiIndex (edge,node)**).
  `[verified:src=classes/incidence_store.py, property_store.py]`
- **Feature surface:** **s-centrality suite (canonical)** — s_betweenness/closeness/harmonic/
  eccentricity; hypergraph modularity (Kumar formulation, `hmod`); spectral clustering; **mod-2
  homology**; **concept lattices (FCA)**; temporal contagion; matching; generative models; extensive
  drawing; **HIF I/O**. `[verified:src=algorithms/metrics/s_centrality_measures.py, hmod]`
- **Distinctive:** the per-incidence/cell-level property model (MultiIndex DataFrame) and the s-
  centrality suite (author-originated reference).

**hypergraphx/HGX** (`HGX-Team/hypergraphx`, v1.8.0, 330★, last push 2026-06-30, BSD-3-Clause)
- **Storage:** dict-based with `_edge_list`/`_reverse_edge_list`/`_weights` + separate
  `_node_metadata`/`_edge_metadata`/**`_incidences_metadata`** dicts + lazy auxiliary structures.
  `[verified:src=core/base.py]`
- **Feature surface (broadest checklist):** weighted/directed/**temporal**/**multiplex**/signed as
  separate core classes; **community detection (the standout)** — Hypergraph-MT, Hy-MMSBM, hy_sc,
  hyperlink_comm, core_periphery; **motifs** (incl. directed); centrality (s- + eigen- +
  sub-hypergraph); shortest paths incl. temporal; dynamics; representations (bipartite/line/clique/
  dual/simplicial); generative models; **filters/sparsification**; I/O; viz.
  `[verified:src=communities/, measures/]`
- **Distinctive:** only lib with all of temporal/multiplex/signed first-class; motif analysis;
  community-detection breadth unmatched.

**DHG/DeepHypergraph** (`iMoonLab/DeepHypergraph`, v0.9.5, 874★, Apache-2.0, alive-slow)
- **Storage:** PyTorch-tensor, GPU/device-aware, integer-indexed vertices only. Caches Laplacians.
  `[verified:src=structure/hypergraphs/hypergraph.py]`
- **Feature surface:** message passing (v2v/v2e/e2v); GNN/HGNN model zoo (HGNN/HGNN+/HyperGCN/HNHN/
  UniGNN/DHCF + graph models GCN/GAT/GIN/etc.); datasets; metrics (ML-task evaluation, NOT graph
  analytics); random generators; viz.
- **Distinctive:** only cleanly-permissive license (Apache-2.0); richest GNN/HGNN model zoo;
  structure-attached cached Laplacians. **Not a graph-analytics library** — no BFS/shortest-path/
  centrality/community/s-walk/etc.

**TopoNetX** (`pyt-team/TopoNetX`, v0.4.0, 274★, MIT, last push 2026-07-29)
- Structure-type breadth: simplicial/cell/combinatorial/**colored hypergraph**/path complex.
  `[verified:src=classes/]` Thin on classical algorithms; oriented to topological DL (pairs with
  TopoEmbeddings/TopoModelX).

### Rust

**yamafaktory/hypergraph** (v4.2.0, 351★, MIT, active) — most relevant for algorithm provenance.
- **Storage:** `AIndexMap<VertexIndex,(V,AIndexSet<HyperedgeIndex>)>` + `AIndexMap<HyperedgeIndex,
  (Vec<VertexIndex>,HE)>` — double-sided IndexMap, monotonic never-reused integer handles.
  `[verified:src=src/core/hypergraph.rs]`
- **The `HypergraphQuery<V,HE>` trait — VERIFIED still the centerpiece.** 9 required primitives:
  `count_vertices`, `count_hyperedges`, `is_empty`, `vertex_indices`, `hyperedge_indices`,
  `get_vertex_weight`, `get_hyperedge_weight`, `get_vertex_hyperedges`, `get_hyperedge_vertices`.
  Adjacency (`get_adjacent_vertices_from/to`) and degrees are *derived defaults*, not required.
  `[verified:src=src/core/query/trait_def.rs]`
- **~40 default algorithms:** BFS/DFS/is_reachable/get_all_paths/random_walk; **is_acyclic/
  topological_sort/strongly_connected_components (Kosaraju)**/connected_components/is_connected/
  **get_transitive_closure**; Dijkstra (3 variants); **k-core** (iterative peeling)/expand_to_graph/
  expand_to_star/**compute_page_rank** (power iteration)/**compute_centrality** (degree+closeness+
  betweenness)/incidence matrices/Laplacian/line graph/dual; orphan detection/endpoints/inclusions/
  k-uniform/**find_cut_vertices** (Tarjan articulation)/nestedness profile.
  `[verified:src=src/core/query/{traversal,structural,paths,projections,properties}.rs]`
- **Persistent/tiered:** `PersistentHypergraph<V,HE>` implements the same trait (feature `persistence`);
  fjall LSM-tree + quick_cache LRU + 4 keyspaces + 16-byte presence-only back-reference keys.
  `[verified:src=src/core/disk/types.rs]`
- **Dead-ends:** directed-only (no undirected primitive); `HE: Into<usize>` weight bound; no
  triangle count/clustering coefficient/s-walk/ML/reification/provenance/versioning.

**mhgl** (`matthagan15/mhgl`, v0.2.4, 9★, MIT) — FxHashMap-backed, first-class **simplicial
operators** (link/boundary_up/boundary_down/skeleton). The only lib with link/boundary operators.
`[verified:src=src/hypergraph.rs]`

**open-hypergraphs** (`hellas-ai/open-hypergraphs`, v0.3.2, 24★, Apache-2.0) — different lineage:
open hypergraphs as combinatorial syntax for string diagrams / categorical DL (consumed by `catgrad`
differentiable array compiler). Not general analytics. Data-parallel, differentiable.

**petgraph — confirmed NOT a hypergraph library** (code search 0 hits). Apache-2.0, 3,979★.

### Julia

**SimpleHypergraphs.jl** (`pszufe/SimpleHypergraphs.jl`, v0.3.4, 86★, MIT)
- **Storage:** `v2he::Vector{Dict{Int,T}}` + `he2v::Vector{Dict{Int,T}}` (two views of one sparse
  incidence matrix) + **first-class `v_meta::Vector{Union{V,Nothing}}` and `he_meta` slots** +
  traits `HasVertexMeta`/`HasHyperedgeMeta`. `[verified:src=src/hypergraph.jl, abstracttypes.jl]`
- **s-walk/s-distance:** `SnodeDistanceDijkstra`/`SedgeDistanceDijkstra` + `diameter` — builds the
  s-adjacency matrix then delegates to `Graphs.dijkstra_shortest_paths`. Source explicitly cites
  HNX as the definitional authority. `[verified:src=src/algorithms/distance.jl]`
- **Community:** `modularity` (strict hypergraph), `findcommunities` (label propagation + CNM-like
  modularity). **Quad clustering** (C4-based, not triangle). `conductance`. `BipartiteView`/dual/
  twosection projections. HIF I/O.

**SimpleDirectedHypergraphs.jl** (`CoReACTER/`, v0.2.0, 3★, brand-new 2026-07-30) — directed
companion, mirrors SimpleHypergraphs' structure, JuMP/GLPK deps (optimization-based paths).

**HyperGraphs.jl** (`lpmdiaz/`, v0.2.0, 44★, stale 2023) — chemistry-flavored; first-class
**oriented `ChemicalHyperEdge` with multiplicities** (`SpeciesSet` with stoichiometric counts).

### C++

**KaHyPar / Mt-KaHyPar** — partition-only, but the **CSR storage spine** is the stealable pattern.
Mt-KaHyPar's `static_hypergraph` (read-only CSR, `{begin,size}` windows, `_valid` flag tombstoning)
vs `dynamic_hypergraph` (mutable) split is a clean tiered-storage reference. MIT (Mt-KaHyPar) /
GPL-3.0 (KaHyPar — ideas only, no code lift). `[verified:src=mt-kahypar/datastructures/static_hypergraph.h]`

**HyperGraphLib** (`alex-87/HyperGraphLib`, 26★, MIT) — only general-purpose C++ hypergraph lib;
**adjacency-matrix** storage (scales O(V·E) — wrong pattern for sparse memory graphs); isomorphism
via Gecode.

**EnTT** (12,971★, MIT) — ECS, not a hypergraph; **sparse-set pools + generational entity IDs** =
the storage-pattern blueprint for Topos's PropertyBag. `[verified:src=sparse_set.hpp, entity.hpp]`

### Java / Go / C# / JS

- **JGraphT** (2,777★) — **independently re-verified: zero "hyper" occurrences in entire repo.**
  Binary-edge only. Algorithm gold standard, but structurally incapable of hypergraphs.
- **Go** — no mature general-purpose hypergraph lib (HypergraphGo pivoted to HoTT, 5★).
- **C# / .NET** — **the niche is empty.** QuikGraph owns pairwise graphs; the only "hypergraph"
  NuGet package besides Topos (MarkupAxis.HyperMap, 790 dl) has a **404 source repo**. This is the
  gap Topos targets.
- **JS/TS** — no mature math hypergraph lib; "hypergraph" on npm is polluted by Web3/crypto
  (Graph Protocol), PCB routing (tscircuit), and viz (d3-hypergraph).

### R

Thin. **HyperG** (frozen 2021, GPL) — spectral embedding/clustering + hypergraph algebra (entropy,
complement); useful as a spectral reference. **rhype** (dormant 2022) — early-stage, explicit
oriented/directed/real-coefficient incidence modeling but algorithms "not yet finished."

---

## 3. Cross-cutting findings (what's worth stealing, by capability)

| Capability | Best source | Relevance to Topos |
|---|---|---|
| **9-primitive → ~40-algorithm trait** | yamafaktory `HypergraphQuery` | Topos already uses this (M1); re-verified |
| **Typed per-element metadata slots** | SimpleHypergraphs.jl (`v_meta`/`he_meta`), HyperGraphs.jl (multiplicity) | Topos's typed properties already cover this |
| **Lazy typed-stat views (`H.nodes.<stat>`)** | **xgi `xgi.stats`** | Worth studying for Topos's typed-property API ergonomics |
| **Per-incidence/cell-level properties** | HNX `PropertyStore` (MultiIndex), HGX `_incidences_metadata` | Topos's `Incidence` cell properties cover this |
| **s-walk / s-centrality family** | **HNX (authoritative)**, xgi (s-line-graph), SH.jl (s-distance) | Topos has basic SWalk; rich family is the M11 gap |
| **Temporal/multiplex/signed** | HGX (only lib with all three) | Future — not v1 for Topos |
| **Community detection / motifs** | HGX (Hy-MMSBM, motifs), HNX (Kumar modularity) | Modularity is good-to-have (oracle-fragmented) |
| **GNN/HGNN models** | DHG | No-need — Topos is a store, not a learning framework |
| **CSR storage spine / static-vs-dynamic split** | Mt-KaHyPar, KaHyPar | Storage-pattern reference; Topos uses sparse-set pools (EnTT) |
| **Persistent/tiered (LSM + LRU)** | yamafaktory `PersistentHypergraph` | Topos M4 already implemented this independently |
| **HIF interchange** | xgi + HNX (both have `hif.py`) | Good-to-have; emerging standard |
| **Simplicial/homology/concept-lattice** | HNX, TopoNetX | No-need (niche, not agent-memory) |
| **Colored/combinatorial complex** | TopoNetX | No-need (different object) |

---

## 4. The oracle question — the crux for hypergraph-native verification

**Direct answer:** for the s-walk family, **yes — HyperNetX is the canonical oracle**, and
unusually cleanly so. The s-walk framework was *defined* by Aksoy/Joslyn/Purvine/Praggastis (EPJ
Data Science 2020), and **the same people wrote HyperNetX**. Competing libraries defer to it
(SimpleHypergraphs.jl's source: *"The concepts of s-distance and s-walk have been defined in the
Python library HyperNetX"*). `[verified:src=pszufe/SimpleHypergraphs.jl/src/algorithms/distance.jl]`

**Oracle viability matrix** for the algorithms GDS can't verify:

| Algorithm | Primary oracle | Independent cross-check | Verdict |
|---|---|---|---|
| s-line-graph | **HNX** `get_linegraph` | **xgi** `to_line_graph(H,s=1)` | **Strong** (2 indep. peer-reviewed impls) |
| s-connected-components | **HNX** `s_connected_components` | (none — xgi lacks it) | Good single-source (HNX is author-origin) |
| s-distance / s-diameter | **HNX** `distance`/`diameter` | **SH.jl** `SnodeDistanceDijkstra` | **Strong** (clean Dijkstra-on-s-adjacency decomposition) |
| s-centralities | **HNX** `s_*_centrality` | (none) | Single-source — HNX is sole impl |
| Tensor/eigenvector centralities | **xgi** centrality module | HAT (partial) | Strong (well-defined tensor formulations) |
| **Hypergraph modularity** | **Depends on definition** | — | **WEAK / fragmented — 3+ competing defs, no canonical oracle** |
| Community detection | HNX `kumar`, xgi `spectral_clustering` | SH.jl label-prop | Moderate (each method differs) |

**The modularity caveat (important):** hypergraph modularity has **3+ competing peer-reviewed
definitions** that give different answers on the same input — Kumar et al. (linear/majority/strict
purity; what HNX ships), Chodrow-Veldt-Benson (AON/differentiated objective; research-code-only,
stale 2022), Kamiński h-Louvain (scalable; Julia, stale 2022). **There is no single canonical
oracle.** Topos must (a) pick one definition and cite it, (b) cross-check only against the matching
repo, (c) document the choice loudly. Recommendation: Kumar (only one with a maintained oracle).

**NetworkX and igraph: confirmed NOT viable hypergraph oracles** — both purely pairwise; users fake
hypergraphs via bipartite representations. Useful only as oracles for *binary projections*.

---

## 5. Honest gaps in this survey

- **License ambiguity:** xgi/HNX/HGX all return `NOASSERTION` in GitHub metadata (non-standard
  LICENSE file headers). PyPI/`pyproject.toml` says BSD-3-Clause for all three; clear before any
  derivative use. DHG (Apache-2.0) and TopoNetX (MIT) are cleanly permissive.
- **Uninspected source:** a handful of minor Rust crates (`hypergraphx` Rust, `rshyper` family,
  `oxgraph-hyper`) and the MarkupAxis.HyperMap C# package (dead source link) are metadata-only
  `[unverified]` — flagged for transparency, but none are competitive.
- **Recency:** star counts/versions are a 2026-07-29/30 snapshot; the libraries are alive and will
  drift. The *patterns* and *algorithm taxonomy* are durable; the metadata will age.

---

## 6. Net assessment for Topos

The fresh pass **strengthens** the base investigation's conclusions rather than overturning them:

1. **The trait pattern (9 primitives → ~40 algorithms) is independently re-verified** as the best
   API-factoring idea in the space, and Topos already uses it.
2. **No library in any language combines** n-ary hyperedges + typed properties + persistence +
   provenance + vector storage + agent-memory focus — Topos's intended feature set remains
   genuinely novel. The survey found *more* libraries and the conclusion held *more* strongly.
3. **The s-walk family is the clear must-have gap** — Topos has a basic `SWalk`; the field shows a
   much richer family (s-components, s-line-graph, s-diameter, s-centrality), and there's now a
   strong canonical oracle (HNX) for it. This drives `ALGORITHM_SPEC.md`'s M11 must-have list.
4. **The C# niche is confirmed empty** — Topos enters an open field; the only competitor
  (MarkupAxis.HyperMap) is unverifiable with a dead source repo.
5. **The modularity caveat is the one place to lower trust expectations** — document the definition
   choice loudly rather than claiming a single canonical answer.

See `docs/ALGORITHM_SPEC.md` for the must-have/good-to-have/no-need bucketing and the per-algorithm
implementation spec derived from this survey.
