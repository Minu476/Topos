using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

public class LabelPropagationTests
{
    [Fact]
    public void DisconnectedComponents_AlwaysGetDifferentLabels()
    {
        var kernel = new HypergraphKernel();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var edgeAB = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edgeAB, a, 0, 0);
        kernel.AddIncidence(edgeAB, b, 0, 1);

        var x = kernel.CreateVertex();
        var y = kernel.CreateVertex();
        var edgeXY = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edgeXY, x, 0, 0);
        kernel.AddIncidence(edgeXY, y, 0, 1);

        var labels = LabelPropagation.DetectCommunities(kernel);

        Assert.Equal(labels[a], labels[b]);
        Assert.Equal(labels[x], labels[y]);
        Assert.NotEqual(labels[a], labels[x]);
    }

    [Fact]
    public void SingleDenseCluster_ConvergesToOneSharedLabel()
    {
        var kernel = new HypergraphKernel();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var c = kernel.CreateVertex();
        var d = kernel.CreateVertex();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge, a, 0, 0);
        kernel.AddIncidence(edge, b, 0, 1);
        kernel.AddIncidence(edge, c, 0, 2);
        kernel.AddIncidence(edge, d, 0, 3);

        var labels = LabelPropagation.DetectCommunities(kernel);

        Assert.Equal(labels[a], labels[b]);
        Assert.Equal(labels[b], labels[c]);
        Assert.Equal(labels[c], labels[d]);
    }

    [Fact]
    public void EmptyGraph_ReturnsEmptyPartition()
    {
        var kernel = new HypergraphKernel();
        Assert.Empty(LabelPropagation.DetectCommunities(kernel));
    }

    [Fact]
    public void IsolatedVertex_KeepsItsOwnLabel()
    {
        var kernel = new HypergraphKernel();
        var isolated = kernel.CreateVertex();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge, a, 0, 0);
        kernel.AddIncidence(edge, b, 0, 1);

        var labels = LabelPropagation.DetectCommunities(kernel);

        Assert.NotEqual(labels[isolated], labels[a]);
    }

    [Fact]
    public void EveryVertex_GetsALabel()
    {
        var kernel = new HypergraphKernel();
        for (int i = 0; i < 10; i++) kernel.CreateVertex();

        var labels = LabelPropagation.DetectCommunities(kernel);

        Assert.Equal(10, labels.Count);
    }
}
