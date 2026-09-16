# The canonical Topos HNX oracle (`docs/ALGORITHM_SPEC.md` §6.1 option (a), decided 2026-09-16).
#
# Pinned on both axes on purpose. The library versions are the ones ALGORITHM_SPEC.md §9 names, so
# a parity result is attributable to a specific oracle rather than to whatever PyPI served that
# day. The interpreter is pinned too because the host's Python is not the project's to choose --
# that independence is the actual argument for the container over a local virtualenv, more than
# isolation is (HNX is a pure library with no credentials and no server, unlike the GDS oracle).
#
# Build:  docker build -t topos-hnx-oracle tests/Topos.Tests.HnxOracle
# Use:    the test project invokes this itself; see docs/HNX_ORACLE_SETUP.md.
FROM python:3.11-slim

RUN pip install --no-cache-dir hypernetx==2.4.3 xgi==0.10.2

# The worker script is mounted read-only at run time rather than copied in, so editing it does not
# mean rebuilding the image -- there is exactly one copy of the oracle logic, in the test project.
WORKDIR /oracle
ENTRYPOINT ["python"]
