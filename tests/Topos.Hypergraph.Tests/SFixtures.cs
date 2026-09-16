using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

/// <summary>
/// Shared hypergraphs for M11 phase 2's s-connectivity tests. These are not arbitrary: both
/// fixtures are the exact inputs `tests/Topos.Tests.HnxOracle` feeds HyperNetX and xgi, so the
/// hand-computed expectations asserted in-process here (T4) and the oracle-parity expectations
/// asserted there (T2) are statements about the same graphs — a disagreement between the two
/// levels is therefore a real disagreement, not a fixture mismatch.
/// </summary>
internal static class SFixtures
{
    internal sealed record Fixture(HypergraphKernel Kernel, IReadOnlyDictionary<Handle, string> Names)
    {
        /// <summary>
        /// The kernel as the query surface. The s-family are default interface methods, so they
        /// are reachable only through <see cref="IHypergraphQuery"/>, not through the concrete
        /// kernel -- the same `IHypergraphQuery kernel = raw;` step every other kernel-algorithm
        /// test in this project already makes.
        /// </summary>
        public IHypergraphQuery Query => Kernel;

        public Handle this[string name] => Names.First(p => p.Value == name).Key;

        /// <summary>Components as name-sets, sorted, so an assertion reads as the mathematical claim.</summary>
        public List<List<string>> NameComponents(IReadOnlyList<IReadOnlyList<Handle>> components) =>
            [.. components.Select(c => c.Select(h => Names[h]).OrderBy(n => n, StringComparer.Ordinal).ToList())
                          .OrderBy(c => string.Join(",", c), StringComparer.Ordinal)];

        /// <summary>An adjacency as name → sorted neighbor names.</summary>
        public Dictionary<string, List<string>> NameAdjacency(IReadOnlyDictionary<Handle, IReadOnlyList<Handle>> adjacency) =>
            adjacency.ToDictionary(
                p => Names[p.Key],
                p => p.Value.Select(h => Names[h]).OrderBy(n => n, StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// A deliberately awkward hypergraph: E1={a,b,c}, E2={b,c,d}, E3={d,e}, E4={f}.
    /// It exercises every edge case the family has at once — an s=2 link (E1/E2 share {b,c}) next
    /// to an s=1-only link (E2/E3 share {d}), a component that splits as s rises, and a
    /// single-member edge E4 that can never be s-adjacent to anything.
    /// </summary>
    public static Fixture Awkward()
    {
        var kernel = new HypergraphKernel();
        var names = new Dictionary<Handle, string>();

        Handle Vertex(string name)
        {
            var h = kernel.CreateVertex();
            names[h] = name;
            return h;
        }

        Handle Edge(string name, params Handle[] members)
        {
            var h = kernel.CreateVertex(VertexRoles.Edge);
            names[h] = name;
            for (int i = 0; i < members.Length; i++) kernel.AddIncidence(h, members[i], 0, i);
            return h;
        }

        var a = Vertex("a"); var b = Vertex("b"); var c = Vertex("c");
        var d = Vertex("d"); var e = Vertex("e"); var f = Vertex("f");

        Edge("E1", a, b, c);
        Edge("E2", b, c, d);
        Edge("E3", d, e);
        Edge("E4", f);

        return new Fixture(kernel, names);
    }

    /// <summary>
    /// A fully 1-connected hypergraph: E1={a,b,c}, E2={b,c,d}, E3={c,d,e}. Needed as a separate
    /// fixture because HNX's <c>diameter</c>/<c>s_eccentricity</c> refuse to answer at all on a
    /// hypergraph that is not globally s-connected, so <see cref="Awkward"/> cannot be used to
    /// pin those two against the oracle — see `IHypergraphQuery.SConnectivity.cs` on that
    /// divergence.
    /// </summary>
    public static Fixture Connected()
    {
        var kernel = new HypergraphKernel();
        var names = new Dictionary<Handle, string>();

        Handle Vertex(string name)
        {
            var h = kernel.CreateVertex();
            names[h] = name;
            return h;
        }

        Handle Edge(string name, params Handle[] members)
        {
            var h = kernel.CreateVertex(VertexRoles.Edge);
            names[h] = name;
            for (int i = 0; i < members.Length; i++) kernel.AddIncidence(h, members[i], 0, i);
            return h;
        }

        var a = Vertex("a"); var b = Vertex("b"); var c = Vertex("c");
        var d = Vertex("d"); var e = Vertex("e");

        Edge("E1", a, b, c);
        Edge("E2", b, c, d);
        Edge("E3", c, d, e);

        return new Fixture(kernel, names);
    }

    /// <summary>
    /// A seeded pseudo-random hypergraph for the metamorphic (T3) properties — deterministic, so a
    /// failure is reproducible from the seed alone rather than being a flake to chase.
    /// </summary>
    public static HypergraphKernel Random(int seed, int vertexCount = 12, int edgeCount = 8, int maxEdgeSize = 5)
    {
        var rng = new Random(seed);
        var kernel = new HypergraphKernel();
        var vertices = new List<Handle>();
        for (int i = 0; i < vertexCount; i++) vertices.Add(kernel.CreateVertex());

        for (int i = 0; i < edgeCount; i++)
        {
            var edge = kernel.CreateVertex(VertexRoles.Edge);
            var members = new HashSet<Handle>();
            int size = 1 + rng.Next(maxEdgeSize);
            while (members.Count < size) members.Add(vertices[rng.Next(vertices.Count)]);

            int ordinal = 0;
            foreach (var m in members) kernel.AddIncidence(edge, m, 0, ordinal++);
        }
        return kernel;
    }
}
