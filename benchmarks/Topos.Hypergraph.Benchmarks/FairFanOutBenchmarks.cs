using BenchmarkDotNet.Attributes;
using Topos.Hypergraph;

namespace Topos.Hypergraph.Benchmarks;

/// <summary>
/// Follow-up to <see cref="IncidenceIndexVsNaiveBenchmarks"/>'s fan-out case: that comparison let
/// the naive baseline do half the work (one direction, no thread-safety) that
/// <see cref="HypergraphKernel"/> actually does (two directions, safe for concurrent readers).
/// This version gives the naive baseline the same job — two dictionaries, one lock each, mirroring
/// the kernel's per-pool granularity — so the ratio reflects real overhead, not different work.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class FairFanOutBenchmarks
{
    [Params(1_000, 20_000)]
    public int N;

    [Benchmark(Baseline = true)]
    public long FairNaive_TwoDirection_LockedDictionary_AppendThenLookup()
    {
        var bySource = new Dictionary<Handle, List<Handle>>();
        var byMember = new Dictionary<Handle, List<Handle>>();
        var sourceLock = new ReaderWriterLockSlim();
        var memberLock = new ReaderWriterLockSlim();

        for (int i = 0; i < N; i++)
        {
            var source = new Handle((uint)i);
            var member = new Handle((uint)(i + 1_000_000));

            sourceLock.EnterWriteLock();
            try
            {
                if (!bySource.TryGetValue(source, out var list)) bySource[source] = list = [];
                list.Add(member);
            }
            finally { sourceLock.ExitWriteLock(); }

            memberLock.EnterWriteLock();
            try
            {
                if (!byMember.TryGetValue(member, out var list)) byMember[member] = list = [];
                list.Add(source);
            }
            finally { memberLock.ExitWriteLock(); }
        }

        long touched = 0;
        for (int i = 0; i < N; i++)
        {
            sourceLock.EnterReadLock();
            try { touched += bySource[new Handle((uint)i)].ToArray().Length; }
            finally { sourceLock.ExitReadLock(); }
        }
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
