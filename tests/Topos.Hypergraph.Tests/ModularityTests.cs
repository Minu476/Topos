using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

public class ModularityTests
{
    [Fact]
    public void AllVerticesInOneCommunity_ModularityIsExactlyZero()
    {
        // Hand-verifiable invariant: when every vertex shares one community, all degree sits in
        // that community, so the sum-of-squares term exactly cancels the internal-edges term by
        // construction (internalEdges/m = 1, sumOfSquares/(4m^2) = (2m)^2/(4m^2) = 1 -> Q = 0).
        var kernel = new HypergraphKernel();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var c = kernel.CreateVertex();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge, a, 0, 0);
        kernel.AddIncidence(edge, b, 0, 1);
        kernel.AddIncidence(edge, c, 0, 2);

        var oneCommunity = kernel.VertexHandles().ToDictionary(h => h, _ => 0);

        Assert.Equal(0.0, Modularity.Compute(kernel, oneCommunity), precision: 10);
    }

    [Fact]
    public void EverySingletonCommunity_ModularityIsNegative()
    {
        // No edge can be internal when every vertex is its own community, but the degree-based
        // null-model term is still positive -- Q must be negative whenever there's at least one edge.
        var kernel = new HypergraphKernel();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge, a, 0, 0);
        kernel.AddIncidence(edge, b, 0, 1);

        var singletons = kernel.VertexHandles()
            .Select((h, i) => (h, i))
            .ToDictionary(p => p.h, p => p.i);

        Assert.True(Modularity.Compute(kernel, singletons) < 0.0);
    }

    [Fact]
    public void EmptyGraph_ModularityIsZero()
    {
        var kernel = new HypergraphKernel();
        Assert.Equal(0.0, Modularity.Compute(kernel, new Dictionary<Handle, int>()));
    }

    [Fact]
    public void TwoDisjointCliques_CorrectPartition_ScoresHigherThanWrongPartition()
    {
        var kernel = new HypergraphKernel();
        var edge1 = kernel.CreateVertex(VertexRoles.Edge);
        var a = kernel.CreateVertex(); var b = kernel.CreateVertex(); var c = kernel.CreateVertex();
        kernel.AddIncidence(edge1, a, 0, 0); kernel.AddIncidence(edge1, b, 0, 1); kernel.AddIncidence(edge1, c, 0, 2);

        var edge2 = kernel.CreateVertex(VertexRoles.Edge);
        var x = kernel.CreateVertex(); var y = kernel.CreateVertex(); var z = kernel.CreateVertex();
        kernel.AddIncidence(edge2, x, 0, 0); kernel.AddIncidence(edge2, y, 0, 1); kernel.AddIncidence(edge2, z, 0, 2);

        var correctPartition = new Dictionary<Handle, int>
        {
            [edge1] = 0, [a] = 0, [b] = 0, [c] = 0,
            [edge2] = 1, [x] = 1, [y] = 1, [z] = 1,
        };
        var wrongPartition = kernel.VertexHandles().ToDictionary(h => h, _ => 0); // everyone lumped together

        double correctQ = Modularity.Compute(kernel, correctPartition);
        double wrongQ = Modularity.Compute(kernel, wrongPartition);

        Assert.True(correctQ > wrongQ);
        Assert.Equal(0.0, wrongQ, precision: 10); // matches the single-community invariant above
    }

    [Fact]
    public void LabelPropagationOutput_ScoresNonNegativelyOnDisconnectedClusters()
    {
        // Integration check: LabelPropagation's own output, fed into Modularity, should score at
        // least as well as the trivial one-community partition for a graph with real structure.
        var kernel = new HypergraphKernel();
        var edge1 = kernel.CreateVertex(VertexRoles.Edge);
        var a = kernel.CreateVertex(); var b = kernel.CreateVertex();
        kernel.AddIncidence(edge1, a, 0, 0); kernel.AddIncidence(edge1, b, 0, 1);

        var edge2 = kernel.CreateVertex(VertexRoles.Edge);
        var x = kernel.CreateVertex(); var y = kernel.CreateVertex();
        kernel.AddIncidence(edge2, x, 0, 0); kernel.AddIncidence(edge2, y, 0, 1);

        var detected = LabelPropagation.DetectCommunities(kernel);
        double q = Modularity.Compute(kernel, detected);

        Assert.True(q >= 0.0);
    }
}
