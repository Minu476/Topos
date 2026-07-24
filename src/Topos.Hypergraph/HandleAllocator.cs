namespace Topos.Hypergraph;

/// <summary>
/// Lock-free monotonic Handle allocator (spec §3.4: "Counter allocation: lock-free monotonic").
/// Never reuses an Index, even after the vertex it named goes dormant — Invariant 1 depends on
/// this. Safe to call from any number of concurrent threads.
/// </summary>
public sealed class HandleAllocator
{
    private uint _next;

    /// <summary>
    /// <paramref name="startingIndex"/> supports M4 persistence (spec §6): reloading a snapshot
    /// must resume allocation *after* every Index the snapshot already contains, or the first
    /// new vertex created post-reload would collide with a loaded one — a direct Invariant 1
    /// violation (Index reuse). <see cref="NextIndex"/> is the paired snapshot-side accessor.
    /// </summary>
    public HandleAllocator(uint startingIndex = 0) => _next = startingIndex;

    public Handle Next() => new(Interlocked.Increment(ref _next) - 1);

    /// <summary>The Index the next <see cref="Next"/> call will return. Snapshot read for persistence — safe under the SWMR model like every other read in the kernel.</summary>
    public uint NextIndex => _next;
}
