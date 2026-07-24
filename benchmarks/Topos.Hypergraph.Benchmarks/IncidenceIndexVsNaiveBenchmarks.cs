using BenchmarkDotNet.Attributes;
using Topos.Hypergraph;

namespace Topos.Hypergraph.Benchmarks;

/// <summary>
/// M0 exit-gate benchmark, relative gate (spec §6 M0-a): <see cref="HypergraphKernel"/>'s
/// incidence index (copy-on-write) vs the naive <c>Dictionary&lt;Handle, List&lt;Handle&gt;&gt;</c>
/// baseline the spec names explicitly.
///
/// Fan-out shape: N distinct sources, one member appended each, then N lookups. This is the
/// benign case for the COW design. The pathological fan-in case (many appends to *one* source)
/// is measured separately in <see cref="FanInPathologyBenchmarks"/> at a much smaller N — see
/// that class's doc comment for why.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class IncidenceIndexVsNaiveBenchmarks
{
    [Params(1_000, 20_000)]
    public int N;

    [Benchmark(Baseline = true)]
    public long Naive_FanOut_AppendThenLookup()
    {
        var d = new Dictionary<Handle, List<Handle>>();
        for (int i = 0; i < N; i++)
        {
            var source = new Handle((uint)i);
            var member = new Handle((uint)(i + 1_000_000));
            if (!d.TryGetValue(source, out var list)) d[source] = list = [];
            list.Add(member);
        }

        long touched = 0;
        for (int i = 0; i < N; i++)
            touched += d[new Handle((uint)i)].Count;
        return touched;
    }

    [Benchmark]
    public long Kernel_FanOut_AppendThenLookup()
    {
        var kernel = new HypergraphKernel();
        for (int i = 0; i < N; i++)
        {
            var source = new Handle((uint)i);
            var member = new Handle((uint)(i + 1_000_000));
            kernel.AddIncidence(source, member, role: 0, ordinal: 0);
        }

        long touched = 0;
        for (int i = 0; i < N; i++)
            touched += kernel.IncidencesFrom(new Handle((uint)i)).Length;
        return touched;
    }
}
