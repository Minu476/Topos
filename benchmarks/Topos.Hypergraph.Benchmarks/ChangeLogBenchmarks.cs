using BenchmarkDotNet.Attributes;
using Topos.Hypergraph;

namespace Topos.Hypergraph.Benchmarks;

/// <summary>
/// M13 §3.4 — what change tracking costs the writer.
///
/// <b>Why this benchmark is mandatory rather than nice-to-have.</b> Every write in Topos pays for
/// this feature, whether or not anyone reads the feed, so it is exactly the situation the M0
/// benchmark gate exists for — and this project has already been burned once in this same area, when
/// a copy-on-write incidence index chasing lock-free reads lost to a plain
/// <see cref="System.Threading.ReaderWriterLockSlim"/> by 5–6× in the benign case and by thousands
/// of times on a hub vertex (<c>docs/M0_BENCHMARK_RESULTS_2026-07-24.md</c>). The change log is
/// built the boring way for that reason; these numbers are what earn or refuse anything cleverer.
///
/// Three questions, one per benchmark pair:
/// <list type="number">
/// <item><b>What does the always-on revision counter cost?</b> Compared against a kernel built
/// before M13 it cannot be measured directly, so <c>Baseline</c> here is the counter-only path —
/// the log disabled — which is what every existing consumer gets.</item>
/// <item><b>What does the log add on top?</b> The same writes with the log enabled.</item>
/// <item><b>Does coalescing hold under a hot handle?</b> Repeated writes to one handle must update
/// a slot in place rather than consume the ring — the exact claim the first implementation got
/// wrong and a unit test caught.</item>
/// </list>
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class ChangeLogBenchmarks
{
    [Params(10_000, 100_000)]
    public int N;

    [Benchmark(Baseline = true)]
    public long CreateVertex_LogDisabled()
    {
        var kernel = new HypergraphKernel();
        for (int i = 0; i < N; i++) kernel.CreateVertex();
        return kernel.CurrentRevision;
    }

    [Benchmark]
    public long CreateVertex_LogEnabled()
    {
        var kernel = new HypergraphKernel();
        kernel.EnableChangeLog(capacity: 4096);
        for (int i = 0; i < N; i++) kernel.CreateVertex();
        return kernel.CurrentRevision;
    }

    [Benchmark]
    public long SetProperty_LogDisabled()
    {
        var kernel = new HypergraphKernel();
        var key = kernel.ResolveProperty<int>("k");
        var handles = new Handle[64];
        for (int i = 0; i < handles.Length; i++) handles[i] = kernel.CreateVertex();
        for (int i = 0; i < N; i++) kernel.SetProperty(key, handles[i & 63], i);
        return kernel.CurrentRevision;
    }

    [Benchmark]
    public long SetProperty_LogEnabled()
    {
        var kernel = new HypergraphKernel();
        kernel.EnableChangeLog(capacity: 4096);
        var key = kernel.ResolveProperty<int>("k");
        var handles = new Handle[64];
        for (int i = 0; i < handles.Length; i++) handles[i] = kernel.CreateVertex();
        for (int i = 0; i < N; i++) kernel.SetProperty(key, handles[i & 63], i);
        return kernel.CurrentRevision;
    }

    /// <summary>
    /// The coalescing claim, measured rather than asserted: N writes to a single handle should cost
    /// a dictionary hit and an in-place slot rewrite each, and should not scale with the ring's
    /// capacity or evict anything. If this tracks <see cref="SetProperty_LogEnabled"/> closely, the
    /// per-handle budget is real.
    /// </summary>
    [Benchmark]
    public long SetProperty_LogEnabled_OneHotHandle()
    {
        var kernel = new HypergraphKernel();
        kernel.EnableChangeLog(capacity: 4096);
        var key = kernel.ResolveProperty<int>("k");
        var hot = kernel.CreateVertex();
        for (int i = 0; i < N; i++) kernel.SetProperty(key, hot, i);
        return kernel.CurrentRevision;
    }

    /// <summary>Read-side cost: what a polling consumer pays to ask "what changed?" on a full ring.</summary>
    [Benchmark]
    public int ChangesSince_FullRing()
    {
        var kernel = new HypergraphKernel();
        kernel.EnableChangeLog(capacity: 4096);
        for (int i = 0; i < 4096; i++) kernel.CreateVertex();

        long cursor = kernel.CurrentRevision - 2048;
        int total = 0;
        for (int i = 0; i < 100; i++) total += kernel.ChangesSince(cursor, out _).Count;
        return total;
    }
}
