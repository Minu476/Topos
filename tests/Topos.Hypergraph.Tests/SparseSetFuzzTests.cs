using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

/// <summary>
/// Differential fuzz testing: <see cref="SparseSet{T}"/> against a plain
/// <see cref="Dictionary{Handle,T}"/> reference model. A small Handle universe forces slot
/// churn (repeated add/remove on the same indices), which is exactly where a sparse/dense
/// indirection bug would surface. Seeded for reproducibility — a failure should be re-runnable,
/// not a one-off flake.
/// </summary>
public class SparseSetFuzzTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(42)]
    [InlineData(1337)]
    public void DifferentialAgainstDictionary_SetRemoveTryGet(int seed)
    {
        var rng = new Random(seed);
        var sut = new SparseSet<int>();
        var model = new Dictionary<Handle, int>();

        for (int op = 0; op < 5000; op++)
        {
            var handle = new Handle((uint)rng.Next(0, 200)); // small universe -> forced churn
            switch (rng.Next(0, 3))
            {
                case 0: // Set
                    int value = rng.Next();
                    sut.Set(handle, value);
                    model[handle] = value;
                    break;

                case 1: // Remove
                    bool sutRemoved = sut.Remove(handle);
                    bool modelRemoved = model.Remove(handle);
                    Assert.Equal(modelRemoved, sutRemoved);
                    break;

                case 2: // TryGet
                    bool sutFound = sut.TryGet(handle, out var sutValue);
                    bool modelFound = model.TryGetValue(handle, out var modelValue);
                    Assert.Equal(modelFound, sutFound);
                    if (modelFound) Assert.Equal(modelValue, sutValue);
                    break;
            }

            Assert.Equal(model.Count, sut.Count);
        }

        foreach (var (handle, value) in model)
        {
            Assert.True(sut.TryGet(handle, out var sutValue));
            Assert.Equal(value, sutValue);
        }
        Assert.Equal(model.Count, sut.Count);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(2024)]
    public void DifferentialAgainstDictionary_LargeSparseUniverse(int seed)
    {
        // A wide, sparse Handle universe (few collisions) exercises the sparse-array growth path
        // instead of the churn path covered above.
        var rng = new Random(seed);
        var sut = new SparseSet<string>();
        var model = new Dictionary<Handle, string>();

        for (int op = 0; op < 2000; op++)
        {
            var handle = new Handle((uint)rng.Next(0, 1_000_000));
            string value = $"v{op}";
            sut.Set(handle, value);
            model[handle] = value;
        }

        Assert.Equal(model.Count, sut.Count);
        foreach (var (handle, value) in model)
        {
            Assert.True(sut.TryGet(handle, out var sutValue));
            Assert.Equal(value, sutValue);
        }
    }
}
