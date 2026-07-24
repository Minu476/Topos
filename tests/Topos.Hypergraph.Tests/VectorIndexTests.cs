using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

public class VectorIndexTests
{
    [Fact]
    public void NearestNeighbors_ReturnsClosestPointsFirst()
    {
        var kernel = new HypergraphKernel();
        var embedding = kernel.ResolveProperty<float[]>("embedding");
        var index = new VectorIndex(kernel, embedding);

        var origin = kernel.CreateVertex();
        kernel.SetProperty(embedding, origin, [0f, 0f]);
        var near = kernel.CreateVertex();
        kernel.SetProperty(embedding, near, [1f, 0f]);
        var far = kernel.CreateVertex();
        kernel.SetProperty(embedding, far, [10f, 0f]);

        var results = index.NearestNeighbors([0f, 0f], k: 3);

        Assert.Equal(3, results.Count);
        Assert.Equal(origin, results[0].Handle);
        Assert.Equal(near, results[1].Handle);
        Assert.Equal(far, results[2].Handle);
        Assert.True(results[0].Distance <= results[1].Distance);
        Assert.True(results[1].Distance <= results[2].Distance);
    }

    [Fact]
    public void NearestNeighbors_KLargerThanDataset_ReturnsEverything()
    {
        var kernel = new HypergraphKernel();
        var embedding = kernel.ResolveProperty<float[]>("embedding");
        var index = new VectorIndex(kernel, embedding);
        var a = kernel.CreateVertex();
        kernel.SetProperty(embedding, a, [1f]);

        var results = index.NearestNeighbors([0f], k: 100);

        Assert.Single(results);
    }

    [Fact]
    public void NearestNeighbors_EmptyIndex_ReturnsEmpty()
    {
        var kernel = new HypergraphKernel();
        var embedding = kernel.ResolveProperty<float[]>("embedding");
        var index = new VectorIndex(kernel, embedding);

        Assert.Empty(index.NearestNeighbors([0f, 0f], k: 5));
    }

    [Fact]
    public void NearestNeighbors_DimensionMismatch_Throws()
    {
        var kernel = new HypergraphKernel();
        var embedding = kernel.ResolveProperty<float[]>("embedding");
        var index = new VectorIndex(kernel, embedding);
        var a = kernel.CreateVertex();
        kernel.SetProperty(embedding, a, [1f, 2f, 3f]);

        Assert.Throws<ArgumentException>(() => index.NearestNeighbors([1f, 2f], k: 1));
    }

    [Fact]
    public void NearestNeighbors_ZeroOrNegativeK_Throws()
    {
        var kernel = new HypergraphKernel();
        var embedding = kernel.ResolveProperty<float[]>("embedding");
        var index = new VectorIndex(kernel, embedding);

        Assert.Throws<ArgumentOutOfRangeException>(() => index.NearestNeighbors([0f], k: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => index.NearestNeighbors([0f], k: -1));
    }

    [Fact]
    public void NearestNeighbors_OnlyConsidersVerticesWithTheEmbeddingPropertySet()
    {
        var kernel = new HypergraphKernel();
        var embedding = kernel.ResolveProperty<float[]>("embedding");
        var index = new VectorIndex(kernel, embedding);

        var withEmbedding = kernel.CreateVertex();
        kernel.SetProperty(embedding, withEmbedding, [0f, 0f]);
        kernel.CreateVertex(); // no embedding property set at all

        var results = index.NearestNeighbors([0f, 0f], k: 10);

        Assert.Single(results);
        Assert.Equal(withEmbedding, results[0].Handle);
    }
}
