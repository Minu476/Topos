# GDS per-algorithm Community/Enterprise tier — Q9

**Date:** 2026-07-24 · **Author:** Claude (lead dev session) · **Status:** Answers spec §5.1 Q9.

## Answer

**All six algorithms Topos needs for the M6 oracle plan are in GDS Community Edition.** The
Community/Enterprise split is **not** primarily about which algorithms you can run — it's about
*scale*: concurrency (CPU core cap), memory efficiency, and catalog capacity.

| Algorithm | Tier | Confidence |
|---|---|---|
| Louvain | Community | High — corroborated twice, see below |
| Label Propagation | Community | High |
| Weakly Connected Components (WCC) | Community | High |
| Strongly Connected Components (SCC) | Community | High |
| Triangle Count | Community | High |
| Local Clustering Coefficient | Community | High |

**Confidence caveat, stated plainly:** direct `WebFetch` of `neo4j.com/docs/graph-data-science/*`
returned **HTTP 403** on every attempt (introduction page, community-detection page, a versioned
`2.15` page) — the same bot-blocking `docs/SPECIFICATION.md §10.4` already noted for the
Medium/Kuzu source. I could not read the primary docs pages directly. What follows instead is
**two independently-phrased web searches**, both search-engine-mediated summaries of neo4j.com
content the search index could read even though direct fetch couldn't, and both landed on the
same conclusion without prompting for it:

> *"The Community Edition includes all algorithms but limits catalog operations to manage graphs
> and models, limits concurrency to maximum 4 CPU cores, and limits the capacity of the model
> catalog to 3 models."* `[unverified:web — search-engine-mediated, direct fetch blocked]`

> *"All core features are available in both editions: 60+ graph algorithms, graph embeddings,
> and machine learning... both editions include all analytics functionality, graph algorithms
> and machine learning methods, although there are differences relating to performance and
> enterprise capabilities... main differences being performance optimizations and enterprise
> features rather than algorithm availability itself."* `[unverified:web — search-engine-mediated,
> direct fetch blocked]`

**What I could directly verify** (GitHub is not behind the same bot-block, so this one is a real
primary-source fetch): the GPLv3/OpenGDS claim already in spec §5.1 is confirmed at the source.

> *"The Neo4j Graph Data Science library as built and distributed by Neo4j includes the sources
> in this repository as well a suite of closed sources... OpenGDS is licensed under the GNU
> Public License version 3.0."*
> `[verified:web=https://github.com/neo4j/graph-data-science/blob/master/README.adoc]`

This directly corroborates spec §5.1's GPLv3 claim from the actual GDS source repo, independent
of the neo4j.com marketing/docs site.

## What this means for the M6 oracle plan

Spec §5.1 already locked GDS as **test-project-only** (no GPLv3 code reaches the production
`Topos.Hypergraph` assembly). Combined with tonight's finding, the residual risk §5.1 flagged —
"if any of these six are Enterprise-only, M6's oracle plan has a gap" — **appears not to
materialize**: none of the six are gated. The practical constraint that *does* apply is the
**4-CPU-core concurrency cap** in Community Edition, which matters for benchmark-scale test runs
(a large synthetic graph run through GDS Community won't parallelize past 4 cores) but not for
correctness — the property-based parity tests (spot-checking Topos's output against GDS's on the
same graph) don't need Enterprise-scale throughput to be valid, just correct.

## Residual open item

I'd still call this **verified-with-caveat, not closed**, given I never got a direct primary-page
read. If a clean primary-source confirmation matters before M6 (e.g. if GDS's tier policy changed
between whatever the search index cached and now), the next attempt should try: (a) the
Neo4j GDS Community/Enterprise comparison PDF at `go.neo4j.com` (fetched tonight but returned
binary-encoded PDF content the fetch tool couldn't parse as text — a text-extraction pass would
resolve it), or (b) asking in the Neo4j community Discourse/Slack directly, which isn't
bot-blocked the way the docs site is.

`[verified:web=https://github.com/neo4j/graph-data-science/blob/master/README.adoc]`
`[unverified:web — per-algorithm tier table above, direct docs-page fetch blocked (403), two
independent search-engine summaries agree]`
