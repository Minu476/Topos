namespace Topos.Hypergraph;

/// <summary>
/// Lock-free monotonic Handle allocator (spec §3.4: "Counter allocation: lock-free monotonic").
/// Never reuses an Index, even after the vertex it named goes dormant — Invariant 1 depends on
/// this. Safe to call from any number of concurrent threads.
/// </summary>
public sealed class HandleAllocator
{
    private uint _next;

    public Handle Next() => new(Interlocked.Increment(ref _next) - 1);
}
