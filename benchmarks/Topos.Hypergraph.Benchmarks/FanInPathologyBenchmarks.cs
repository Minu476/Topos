using BenchmarkDotNet.Attributes;
using Topos.Hypergraph;

namespace Topos.Hypergraph.Benchmarks;

/// <summary>
/// Demonstrates the O(N²) fan-in cost flagged in <c>HypergraphKernel.Append</c>'s doc comment:
/// N appends into the *same* incidence source, each copying the whole
/// <see cref="System.Collections.Immutable.ImmutableArray{T}"/>. This is the hub-vertex case —
/// a well-connected concept with many incident members. Kept to a small N range specifically to
/// make the quadratic curve visible without a multi-minute run: watch how
/// <c>Kernel_FanIn_AppendToOneSource</c>'s time-per-N-unit grows as N grows, while the naive
/// baseline's stays flat.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class FanInPathologyBenchmarks
{
    [Params(500, 2_000, 8_000)]
    public int N;

    [Benchmark(Baseline = true)]
    public long Naive_FanIn_AppendToOneSource()
    {
        var source = new Handle(0);
        var list = new List<Handle>();
        for (int i = 0; i < N; i++)
            list.Add(new Handle((uint)(i + 1)));
        return list.Count;
    }

    [Benchmark]
    public long Kernel_FanIn_AppendToOneSource()
    {
        var kernel = new HypergraphKernel();
        var source = new Handle(0);
        for (int i = 0; i < N; i++)
            kernel.AddIncidence(source, new Handle((uint)(i + 1)), role: 0, ordinal: i);
        return kernel.IncidencesFrom(source).Length;
    }
}
