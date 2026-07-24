using BenchmarkDotNet.Attributes;
using Topos.Hypergraph;

namespace Topos.Hypergraph.Benchmarks;

/// <summary>
/// M0 exit-gate benchmark, relative gate (spec §6 M0-a): <see cref="SparseSet{T}"/> vs the naive
/// <c>Dictionary&lt;Handle, T&gt;</c> baseline the spec names explicitly, for the vertex/property
/// -pool access pattern — sequential Set over N handles, then sequential Get over the same N.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class SparseSetVsDictionaryBenchmarks
{
    [Params(1_000, 100_000)]
    public int N;

    private Handle[] _handles = [];

    [GlobalSetup]
    public void Setup() => _handles = Enumerable.Range(0, N).Select(i => new Handle((uint)i)).ToArray();

    [Benchmark(Baseline = true)]
    public long Dictionary_SetThenGet()
    {
        var d = new Dictionary<Handle, long>();
        foreach (var h in _handles) d[h] = h.Index;

        long sum = 0;
        foreach (var h in _handles) sum += d[h];
        return sum;
    }

    [Benchmark]
    public long SparseSet_SetThenGet()
    {
        var s = new SparseSet<long>();
        foreach (var h in _handles) s.Set(h, h.Index);

        long sum = 0;
        foreach (var h in _handles) { s.TryGet(h, out var v); sum += v; }
        return sum;
    }
}
