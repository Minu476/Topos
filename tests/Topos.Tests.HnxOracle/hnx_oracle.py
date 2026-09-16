"""Topos's HyperNetX/xgi oracle worker (`docs/ALGORITHM_SPEC.md` §3.1, §6.1).

One JSON request on stdin, one JSON response on stdout. The transport is deliberately this dumb:
it is identical whether the interpreter running this file lives in the `topos-hnx-oracle`
container (the canonical path) or in a local virtualenv (the `TOPOS_HNX_ORACLE_PYTHON` escape
hatch), so the two options in ALGORITHM_SPEC.md §6.1 differ only in how the interpreter is found,
not in what it does. See `docs/HNX_ORACLE_SETUP.md`.

A request is:
    {"op": ..., "hypergraph": {"<edge>": ["<node>", ...]}, "s": 1, "domain": "vertices"|"hyperedges", ...}

A response is either {"result": ...} or {"error": "<message>"}. Errors are *reported*, never
raised, because some of them are the finding: HNX refuses to compute a diameter or an eccentricity
on a hypergraph that is not globally s-connected, and Topos's parity tests assert that refusal
rather than silently skipping the case.
"""

import json
import sys


def _edges_flag(request):
    domain = request.get("domain", "vertices")
    if domain not in ("vertices", "hyperedges"):
        raise ValueError(f"unknown domain {domain!r}")
    return domain == "hyperedges"


def _adjacency(line_graph):
    return {str(n): sorted(str(m) for m in line_graph.neighbors(n)) for n in line_graph.nodes}


def op_ping(request, hnx, xgi):
    return {"hypernetx": hnx.__version__, "xgi": xgi.__version__}


def op_s_connected_components(request, hnx, xgi):
    H = hnx.Hypergraph(request["hypergraph"])
    # return_singletons=True is the comparable mode: HNX's default drops singleton components,
    # while Topos reports them (a node that s-isolates at this s is exactly what distinguishes
    # one s from the next). Same partition either way -- this only stops HNX hiding part of it.
    components = H.s_connected_components(
        s=request["s"], edges=_edges_flag(request), return_singletons=True
    )
    return {"components": sorted(sorted(str(n) for n in c) for c in components)}


def op_linegraph(request, hnx, xgi):
    H = hnx.Hypergraph(request["hypergraph"])
    return {"adjacency": _adjacency(H.get_linegraph(s=request["s"], edges=_edges_flag(request)))}


def op_xgi_linegraph(request, hnx, xgi):
    # xgi's line graph is hyperedge-noded only -- it has no `edges=False` dual. This is the second
    # independent lineage in ALGORITHM_GAP_LIST.md §4.3's differential (T2) argument.
    X = xgi.Hypergraph(request["hypergraph"])
    return {"adjacency": _adjacency(xgi.to_line_graph(X, s=request["s"]))}


def op_distance(request, hnx, xgi):
    H = hnx.Hypergraph(request["hypergraph"])
    source, target = request["source"], request["target"]
    d = (
        H.edge_distance(source, target, s=request["s"])
        if _edges_flag(request)
        else H.distance(source, target, s=request["s"])
    )
    # HNX reports an unreachable pair as inf; Topos reports null. Normalize here so the C# side
    # compares like with like, and record the mapping in one place instead of at each call site.
    return {"distance": None if d == float("inf") else int(d)}


def op_diameter(request, hnx, xgi):
    H = hnx.Hypergraph(request["hypergraph"])
    d = H.edge_diameter(s=request["s"]) if _edges_flag(request) else H.diameter(s=request["s"])
    return {"diameter": int(d)}


def op_eccentricity(request, hnx, xgi):
    from hypernetx.algorithms import s_centrality_measures as sc

    H = hnx.Hypergraph(request["hypergraph"])
    scores = sc.s_eccentricity(H, s=request["s"], edges=_edges_flag(request), return_singletons=True)
    return {"eccentricity": {str(k): int(v) for k, v in scores.items()}}


OPS = {
    "ping": op_ping,
    "s_connected_components": op_s_connected_components,
    "linegraph": op_linegraph,
    "xgi_linegraph": op_xgi_linegraph,
    "distance": op_distance,
    "diameter": op_diameter,
    "eccentricity": op_eccentricity,
}


def main():
    try:
        request = json.load(sys.stdin)
        import warnings

        warnings.filterwarnings("ignore")
        import hypernetx as hnx
        import xgi

        op = OPS.get(request.get("op"))
        if op is None:
            raise ValueError(f"unknown op {request.get('op')!r}")
        response = {"result": op(request, hnx, xgi)}
    except Exception as exc:  # reported, not raised -- see this module's docstring
        response = {"error": f"{type(exc).__name__}: {exc}"}

    # stdout carries the protocol and nothing else; anything a library prints goes to stderr.
    sys.stdout.write(json.dumps(response))


if __name__ == "__main__":
    main()
