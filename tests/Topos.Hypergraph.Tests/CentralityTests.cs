using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

public class CentralityTests
{
    /// <summary>Single 3-member hyperedge: {edge, a, b, c} form K4 under BipartiteAdjacency (TriangleCount's own C(4,3)=4 shape) — every pair directly adjacent.</summary>
    private static (HypergraphKernel Kernel, Handle Edge, Handle A, Handle B, Handle C) BuildK4()
    {
        var kernel = new HypergraphKernel();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var c = kernel.CreateVertex();
        kernel.AddIncidence(edge, a, 0, 0);
        kernel.AddIncidence(edge, b, 0, 1);
        kernel.AddIncidence(edge, c, 0, 2);
        return (kernel, edge, a, b, c);
    }

    /// <summary>Two 2-member hyperedges sharing vertex B: a "bowtie" of two triangles {e1,a,b} and {e2,b,c} joined at b — the standard textbook shape for a non-trivial betweenness golden case.</summary>
    private static (HypergraphKernel Kernel, Handle E1, Handle A, Handle B, Handle E2, Handle C) BuildBowtie()
    {
        var kernel = new HypergraphKernel();
        var e1 = kernel.CreateVertex(VertexRoles.Edge);
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        kernel.AddIncidence(e1, a, 0, 0);
        kernel.AddIncidence(e1, b, 0, 1);

        var e2 = kernel.CreateVertex(VertexRoles.Edge);
        var c = kernel.CreateVertex();
        kernel.AddIncidence(e2, b, 0, 0);
        kernel.AddIncidence(e2, c, 0, 1);

        return (kernel, e1, a, b, e2, c);
    }

    [Fact]
    public void Degree_EveryVertexInK4_IsThree()
    {
        var (kernel, edge, a, b, c) = BuildK4();
        var degree = Centrality.Degree(kernel);

        Assert.Equal(3, degree[edge]);
        Assert.Equal(3, degree[a]);
        Assert.Equal(3, degree[b]);
        Assert.Equal(3, degree[c]);
    }

    [Fact]
    public void Degree_Bowtie_SharedVertexHasDoubleTheDegreeOfLeaves()
    {
        var (kernel, e1, a, b, e2, c) = BuildBowtie();
        var degree = Centrality.Degree(kernel);

        Assert.Equal(2, degree[e1]);
        Assert.Equal(2, degree[a]);
        Assert.Equal(4, degree[b]); // e1, a, e2, c
        Assert.Equal(2, degree[e2]);
        Assert.Equal(2, degree[c]);
    }

    [Fact]
    public void Degree_IsolatedVertex_IsZero()
    {
        var kernel = new HypergraphKernel();
        var isolated = kernel.CreateVertex();

        Assert.Equal(0, Centrality.Degree(kernel)[isolated]);
    }

    [Fact]
    public void Closeness_EveryVertexInK4_IsOne()
    {
        // Fully connected on 4 vertices: each reaches k=3 others at distance 1, so k/sum = 3/3 = 1.
        var (kernel, edge, a, b, c) = BuildK4();
        var closeness = Centrality.Closeness(kernel);

        Assert.Equal(1.0, closeness[edge], precision: 10);
        Assert.Equal(1.0, closeness[a], precision: 10);
        Assert.Equal(1.0, closeness[b], precision: 10);
        Assert.Equal(1.0, closeness[c], precision: 10);
    }

    [Fact]
    public void Closeness_TwoDisjointK4s_ScoreIdenticallyToOneK4()
    {
        // The whole point of GDS's default (non-Wasserman-Faust) formula: each vertex is scored
        // against its OWN reachable set (k), not the graph's total vertex count -- so a second,
        // totally disconnected K4 elsewhere in the same graph must not change these scores at all.
        var (kernel, edge, a, b, c) = BuildK4();
        var edge2 = kernel.CreateVertex(VertexRoles.Edge);
        var x = kernel.CreateVertex(); var y = kernel.CreateVertex(); var z = kernel.CreateVertex();
        kernel.AddIncidence(edge2, x, 0, 0); kernel.AddIncidence(edge2, y, 0, 1); kernel.AddIncidence(edge2, z, 0, 2);

        var closeness = Centrality.Closeness(kernel);

        Assert.Equal(1.0, closeness[edge], precision: 10);
        Assert.Equal(1.0, closeness[a], precision: 10);
        Assert.Equal(1.0, closeness[edge2], precision: 10);
        Assert.Equal(1.0, closeness[x], precision: 10);
    }

    [Fact]
    public void Closeness_IsolatedVertex_IsZero()
    {
        var kernel = new HypergraphKernel();
        var isolated = kernel.CreateVertex();

        Assert.Equal(0.0, Centrality.Closeness(kernel)[isolated]);
    }

    [Fact]
    public void Closeness_EmptyGraph_ReturnsEmptyResult()
    {
        var kernel = new HypergraphKernel();
        Assert.Empty(Centrality.Closeness(kernel));
    }

    [Fact]
    public void Betweenness_Bowtie_SharedVertexScoresFour_EveryoneElseScoresZero()
    {
        // Standard bowtie result: b is the sole shortest-path intermediary for the 4 cross-triangle
        // pairs (e1,e2), (e1,c), (a,e2), (a,c) -- every other pair is directly adjacent.
        var (kernel, e1, a, b, e2, c) = BuildBowtie();
        var betweenness = Centrality.Betweenness(kernel);

        Assert.Equal(4.0, betweenness[b], precision: 10);
        Assert.Equal(0.0, betweenness[e1], precision: 10);
        Assert.Equal(0.0, betweenness[a], precision: 10);
        Assert.Equal(0.0, betweenness[e2], precision: 10);
        Assert.Equal(0.0, betweenness[c], precision: 10);
    }

    [Fact]
    public void Betweenness_K4_EveryVertexScoresZero()
    {
        // Complete graph: every pair is directly adjacent, so no vertex is ever a strict
        // shortest-path intermediary.
        var (kernel, edge, a, b, c) = BuildK4();
        var betweenness = Centrality.Betweenness(kernel);

        Assert.All(new[] { edge, a, b, c }, v => Assert.Equal(0.0, betweenness[v], precision: 10));
    }

    [Fact]
    public void Betweenness_EmptyGraph_ReturnsEmptyResult()
    {
        var kernel = new HypergraphKernel();
        Assert.Empty(Centrality.Betweenness(kernel));
    }
}
