# GDS-parity oracle setup

**Date:** 2026-07-24 · **Status:** Working, verified end-to-end (`tests/Topos.Tests.GdsOracle`).

Spec §5 / §6 M1 calls for a Neo4j GDS oracle to verify Topos's standard graph algorithms
independently. This is the disposable container the harness expects, and how it was built.

## Why a throwaway container, not the host Neo4j install

This machine already has Neo4j installed via Homebrew, actively used by other projects — its
default `neo4j` database has **~6 million existing nodes** and its `fsdedb` database is FSDE's.
Community Edition only supports one user database, so there's no safe way to carve out isolated
space inside that instance. The GDS-parity harness instead runs against a **fully separate Docker
container** on non-default ports, so it can never collide with real project data.

**This isolation also matters outside of data collision.** During the Topos codebase review
(2026-07-24), a separate host install — Neo4j Desktop, Enterprise edition — was found bound to
`0.0.0.0` on ports 7687/7474 with `dbms.security.auth_enabled=false`, i.e. reachable from the LAN
with no credentials. That instance has no connection to this harness (no env var ever points at
it; `Neo4jTestConfig.Default` only resolves to the Docker container above) and was unaffected by
the GDS-parity work either way, but it's rebound to `127.0.0.1`-only now for the same reason this
harness avoids the host install: the two Neo4j instances on this machine should stay isolated from
each other and from the network, not just from each other's data.

**Follow-up (same day):** auth was also re-enabled on that host instance
(`dbms.security.auth_enabled=true`, was `false`), and its `neo4j` user password was rotated after
the prior password was typed into a chat session — treated as compromised on principle, not because
of any observed misuse. New credential lives only in `~/.secrets` (`NEO4J_PASSWORD`, chmod 600, not
committed anywhere). Verified post-rotation: unauthenticated connections are rejected, the old
password no longer authenticates, the new one does, and `tests/Topos.Tests.GdsOracle` (which never
touched this instance) still passes 9/9 against its own Docker container. None of this required any
change to the Docker oracle itself.

**Downstream check (same day):** re-enabling auth on the host instance is a real behavior change
for anything else on this machine that talks to it with real (not mocked) credentials, so both
other local Neo4j consumers were checked and confirmed unaffected — their test suites pass clean
against the rebound + rotated instance. One had a stale credential file, updated to resolve from
the same Keychain entry at shell-start instead.

**Root-cause fix (same day):** the stale `.env` copy above was a symptom, not a one-off — every
consumer that hardcodes a literal Neo4j password on this machine goes stale on the next rotation.
Fixed at the source: the password now lives in one place, macOS Keychain (`security` service
`neo4j-desktop`), and `~/.secrets` resolves `NEO4J_PASSWORD` from it at shell-start instead of
storing it as plaintext. RLB and FSDE's CLI/tests inherit it via the shell as before. The one
consumer that doesn't run under a shell — FSDE's `com.richlearning.fsde.daemon` launchd agent —
got a small wrapper (`~/.fsde/bin/run-daemon.sh`) that resolves the same Keychain entry and execs
the daemon; the plist's `ProgramArguments` now points at the wrapper instead of `dotnet` directly.
FSDE's `.env` no longer carries a `NEO4J_PASSWORD` line at all (confirmed safe: `Program.cs`'s
`.env` loader explicitly never overwrites an already-set env var). Verified with two full
`launchctl bootout`/`bootstrap` cycles — `"Neo4j connectivity verified"`, no auth errors — and
FSDE's real (non-mocked) Neo4j test suite, 15/15. Future rotation is one `security
add-generic-password ... -U` call plus a daemon restart, not a grep across repos.

## The container

```bash
docker run -d \
  --name topos-gds-oracle \
  -p 17687:7687 \
  -p 17474:7474 \
  -e NEO4J_AUTH=neo4j/toposdev123 \
  -e NEO4J_PLUGINS='["graph-data-science"]' \
  -e NEO4J_dbms_security_procedures_unrestricted='gds.*' \
  -e NEO4J_dbms_security_procedures_allowlist='gds.*' \
  neo4j:2026.06-community
```

- **Ports 17687 (Bolt) / 17474 (HTTP)** — offset from Neo4j's defaults (7687/7474) specifically so
  it never conflicts with a locally-running Neo4j instance (this machine has one).
- **`NEO4J_PLUGINS='["graph-data-science"]'`** — the official Neo4j Docker image resolves and
  installs GDS automatically at container start; no manual jar download needed for the
  containerized path (the bare-metal Homebrew path, used earlier while debugging port/db
  isolation, needed a manual jar from
  `https://github.com/neo4j/graph-data-science/releases/download/<version>/neo4j-graph-data-science-<version>.jar`
  into the plugins directory — keeping that path documented here in case Docker isn't available
  in some environment).
- **GPLv3 isolation (spec §5.1) still holds**: nothing about this container touches
  `src/Topos.Hypergraph`. Only `tests/Topos.Tests.GdsOracle` references `Neo4j.Driver`, and that
  driver itself is Apache-2.0 — it just speaks Bolt to whatever's on the other end. The GPLv3 code
  runs entirely server-side in Neo4j, never linked into the Topos assembly.
- **Restart after a machine/Docker restart:** `docker start topos-gds-oracle` (state persists in
  the container's own layer; ~10s to become ready — `Neo4jTestConfig.IsReachableAsync` polls with
  a 3s timeout per attempt, so the harness fails soft, not hard, if it's not up yet).
- **Recreate from scratch** (e.g. after `docker rm`): re-run the `docker run` command above.
  Nothing persists that needs backing up — the harness clears its own data before and after every
  test (`ProjectionEngine.ClearAsync`).

## Running the harness

```bash
dotnet test tests/Topos.Tests.GdsOracle
```

Connects to `bolt://localhost:17687` / `neo4j` / `toposdev123` by default. Override via
`TOPOS_GDS_ORACLE_URI` / `_USER` / `_PASSWORD` / `_DATABASE` env vars if needed. **Skips
gracefully (passes trivially, does not fail) if the oracle isn't reachable** — same convention
RLB's own Neo4j integration tests use (`Neo4jTestConfig.TryLoad()` in
`RichLearning.V2.Tests/EdgeThetaPersistenceTests.cs`), so this suite never breaks CI or another
developer's machine that hasn't stood up the container.

## The one real bug this setup caught

`HypergraphKernel.GetBfs` treats hyperedge co-membership as symmetric (walks *against* the stored
`Incidence.Source→Member` direction to find "which edges is this vertex on," then *with* it to
find co-members). The first version of the GDS projection stored `:INCIDENT` as a **directed**
relationship, matching Topos's storage — and GDS's default (`NATURAL`) graph projection only
follows stored direction, so `gds.bfs.stream` from a pure-member vertex (no outgoing `INCIDENT`
edge) dead-ended at the start node immediately. Fixed by projecting with
`orientation: 'UNDIRECTED'`. Full reasoning in `ProjectionEngine`'s class doc and
`BfsGdsParityTests.RunGdsBfsAsync`'s inline comment. This is exactly the kind of bug the M0
benchmark gate and this M1 GDS-parity gate exist to catch — a plausible-looking design choice that
breaks on contact with an independent, real implementation.
