# HNX-parity oracle setup

**Date:** 2026-09-16 · **Status:** Working, verified end-to-end against live HyperNetX 2.4.3 and
xgi 0.10.2 (`tests/Topos.Tests.HnxOracle`, 10/10 passing).

`docs/ALGORITHM_SPEC.md` §3 calls for a second oracle alongside Neo4j GDS, because GDS
*structurally cannot* verify M11 phase 2's algorithms: "these two hyperedges share at least `s`
common members" has no meaning in a binary projection. HyperNetX is the answer the spec argues
for, on unusually strong grounds — the s-walk framework was defined by Aksoy, Joslyn, Purvine and
Praggastis (EPJ Data Science 2020), and the same people wrote HNX. This is the container that
harness expects, and how it was built.

Sibling document: `docs/GDS_ORACLE_SETUP.md`. The conventions are deliberately the same; the
differences below are the ones that are real.

## Why a container, when HNX is just a library

The GDS oracle is containerized mainly for **isolation** — this machine has a Homebrew Neo4j with
other projects' data in it, and the harness must never touch it. None of that applies here: HNX is
a pure Python library, with no server, no ports and no credentials.

The argument for the container here is **attribution** instead. A parity result is only worth
something if you can say which oracle produced it. `python3` on a developer's PATH is not the
project's to pin — it moves, it differs per machine, and `pip install hypernetx` resolves to
whatever PyPI serves that day. The image pins the interpreter (3.11) and both libraries to the
exact versions `ALGORITHM_SPEC.md` §9 names, so "Topos agrees with HNX" is a statement about a
specific HNX rather than about an ambient one.

That is also why `ALGORITHM_SPEC.md` §6.1's two options — Docker sidecar versus local Python
subprocess — turned out not to be a real fork in the end (`docs/DECISIONS.md`, 2026-09-16). HNX has
nothing to be a sidecar *to*: each request runs the worker once and it exits. The two options are
the same code path with the interpreter located differently, so both are supported, and the
container is simply the default.

## The container

```bash
docker build -t topos-hnx-oracle tests/Topos.Tests.HnxOracle
```

- **`python:3.11-slim` + `hypernetx==2.4.3` + `xgi==0.10.2`** — the versions `ALGORITHM_SPEC.md` §9
  cites. Both libraries are permissively licensed (HNX is 3-Clause BSD; GitHub reports
  `NOASSERTION` only because its LICENSE is a `.rst` file, while the SPDX identifier in its
  `pyproject.toml` is BSD-3-Clause), so there is no GPLv3-style isolation concern to manage the way
  there is with GDS. The container still keeps `src/Topos.Hypergraph` free of any Python dependency,
  which is the property that actually matters.
- **The worker script is mounted, not copied in.** `tests/Topos.Tests.HnxOracle/hnx_oracle.py` is
  bind-mounted read-only at run time from the test project's output directory. Editing the oracle
  logic therefore does not mean rebuilding the image, and there is exactly one copy of that logic.
- **Nothing persists.** The container runs per request with `--rm`; there is no state to back up,
  reset or leak between tests.
- **Rebuild** after changing the Dockerfile, or to move the pinned versions (which is a decision,
  not a chore — see the attribution argument above).

## Running the harness

```bash
dotnet test tests/Topos.Tests.HnxOracle
```

Uses the `topos-hnx-oracle` image by default. Two environment variables override that:

- `TOPOS_HNX_ORACLE_IMAGE` — a different image tag.
- `TOPOS_HNX_ORACLE_PYTHON` — an absolute path to a local interpreter that has `hypernetx` and
  `xgi` installed, bypassing Docker entirely. Useful on a machine without Docker, and the reason
  spec §6.1's option (b) did not need to be written off. Both paths are verified working.

**Skips gracefully (passes trivially, does not fail) when no oracle is reachable** — the same
convention `tests/Topos.Tests.GdsOracle` uses, so this suite never breaks CI or a developer's
machine that has not built the image. The reachability probe runs once per test run rather than
once per test, because a cold `docker run` would otherwise dominate the suite's runtime.

## The protocol

One JSON request on stdin, one JSON response on stdout, one process per request:

```bash
echo '{"op":"ping"}' \
  | docker run --rm -i -v "$PWD/tests/Topos.Tests.HnxOracle:/oracle:ro" topos-hnx-oracle /oracle/hnx_oracle.py
# {"result": {"hypernetx": "2.4.3", "xgi": "0.10.2"}}
```

A response is either `{"result": ...}` or `{"error": "..."}`. **The worker reports exceptions
instead of raising them**, because one of them is a finding rather than a failure — see below.

## Two things this setup pinned down that prose had not

**1. The oracle's node-set and singleton conventions are not the obvious ones.** HNX's
`s_connected_components` defaults to `return_singletons=False`, so it silently omits every
component of size one — and its default `edges=True` partitions *hyperedges*, not vertices. Topos
reports singletons and defaults to vertices. Neither is wrong; comparing them without noticing
would have produced a confident, meaningless parity result. The worker asks for
`return_singletons=True` explicitly, and the request carries the domain, so both sides are known to
be talking about the same node set.

**2. HNX refuses to answer for a disconnected hypergraph; Topos answers.** `diameter(s)`,
`edge_diameter(s)` and `s_eccentricity` all raise `HyperNetXError: Hypergraph is not s-connected`
unless the *whole* hypergraph is s-connected, because they defer to NetworkX's whole-graph
functions. `ALGORITHM_SPEC.md` §5.3 asks for something else — "the maximum s-distance over all
reachable pairs" — which stays defined on every input. Topos implements the spec, and
`SDistanceAndDiameterHnxParityTests` asserts **both halves**: parity on an s-connected fixture, and
HNX's refusal on a disconnected one. Pinning the divergence means that if a future HNX release
starts answering here, the test fails and the difference gets revisited, rather than the two
quietly drifting.
