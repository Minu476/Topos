using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

/// <summary>
/// M13 §3.1 — the reduction anchor, and the closest thing this milestone has to an oracle.
///
/// A change feed has no external reference implementation to compare against. But it makes a claim
/// that <i>can</i> be checked independently: capture the kernel's full state, mutate it, capture the
/// state again, diff the two by brute force, and the feed must have reported every handle the diff
/// found. <see cref="Capture"/>/<see cref="Diff"/> below are written the dumbest possible way — walk
/// everything, compare everything — because an oracle's job is to be obviously correct, not fast.
///
/// <b>The contract being checked is asymmetric, deliberately.</b> The feed may over-report (a
/// consumer re-reads something unchanged, which is merely wasteful) but must never under-report (a
/// consumer misses a change, which is a correctness bug in every consumer at once). So completeness
/// is asserted against arbitrary mutation sequences, and exact equality only against sequences with
/// no redundant writes, where the two notions provably coincide.
/// </summary>
public class ChangeLogDiffParityTests
{
    private sealed record KernelState(
        Dictionary<Handle, Vertex> Vertices,
        Dictionary<Handle, List<Incidence>> Incidences,
        Dictionary<Handle, int> Ints,
        Dictionary<Handle, string> Strings);

    private static KernelState Capture(HypergraphKernel kernel, PropertyKey<int> intKey, PropertyKey<string> strKey)
    {
        var vertices = new Dictionary<Handle, Vertex>();
        var incidences = new Dictionary<Handle, List<Incidence>>();
        foreach (var h in kernel.VertexHandles())
        {
            if (kernel.TryGetVertex(h, out var v)) vertices[h] = v;
            incidences[h] = [.. kernel.IncidencesFrom(h)];
        }
        return new KernelState(
            vertices,
            incidences,
            kernel.EnumerateProperty(intKey).ToDictionary(e => e.Handle, e => e.Value),
            kernel.EnumerateProperty(strKey).ToDictionary(e => e.Handle, e => e.Value));
    }

    /// <summary>Every handle that differs between two captures, by any observable measure.</summary>
    private static HashSet<Handle> Diff(KernelState before, KernelState after)
    {
        var changed = new HashSet<Handle>();
        var everyHandle = new HashSet<Handle>(before.Vertices.Keys);
        everyHandle.UnionWith(after.Vertices.Keys);

        foreach (var h in everyHandle)
        {
            bool inBefore = before.Vertices.TryGetValue(h, out var vBefore);
            bool inAfter = after.Vertices.TryGetValue(h, out var vAfter);
            if (inBefore != inAfter || (inBefore && vBefore != vAfter)) { changed.Add(h); continue; }

            var iBefore = before.Incidences.GetValueOrDefault(h) ?? [];
            var iAfter = after.Incidences.GetValueOrDefault(h) ?? [];
            if (!iBefore.SequenceEqual(iAfter)) { changed.Add(h); continue; }

            if (before.Ints.GetValueOrDefault(h, int.MinValue) != after.Ints.GetValueOrDefault(h, int.MinValue))
            { changed.Add(h); continue; }

            if (before.Strings.GetValueOrDefault(h) != after.Strings.GetValueOrDefault(h)) changed.Add(h);
        }

        // Incidences also land on the member side, which the per-source walk above does not see.
        foreach (var (source, list) in after.Incidences)
        {
            var beforeList = before.Incidences.GetValueOrDefault(source) ?? [];
            foreach (var incidence in list.Skip(beforeList.Count)) changed.Add(incidence.Member);
        }
        return changed;
    }

    [Fact]
    public void FeedNeverMissesAChangeTheDiffFinds()
    {
        foreach (int seed in (int[])[3, 11, 47, 99, 256])
        {
            var kernel = new HypergraphKernel();
            kernel.EnableChangeLog(8192);
            var intKey = kernel.ResolveProperty<int>("i");
            var strKey = kernel.ResolveProperty<string>("s");

            var handles = new List<Handle> { kernel.CreateVertex(), kernel.CreateVertex(VertexRoles.Edge) };
            var before = Capture(kernel, intKey, strKey);
            long cursor = kernel.CurrentRevision;

            var rng = new Random(seed);
            for (int i = 0; i < 250; i++)
            {
                var pick = handles[rng.Next(handles.Count)];
                switch (rng.Next(7))
                {
                    case 0: handles.Add(kernel.CreateVertex()); break;
                    case 1: handles.Add(kernel.CreateVertex(VertexRoles.Edge)); break;
                    case 2: kernel.AddIncidence(handles[rng.Next(handles.Count)], pick, (byte)rng.Next(3), i); break;
                    case 3: kernel.SetProperty(intKey, pick, rng.Next(5)); break;      // may repeat a value
                    case 4: kernel.SetProperty(strKey, pick, $"v{rng.Next(5)}"); break; // may repeat a value
                    case 5: kernel.SetDormant(pick); break;
                    default: kernel.Reactivate(pick); break;
                }
            }

            var after = Capture(kernel, intKey, strKey);
            var expected = Diff(before, after);
            var reported = kernel.ChangesSince(cursor, out bool overflowed).Select(c => c.Handle).ToHashSet();

            Assert.False(overflowed);
            Assert.Subset(reported, expected);   // every handle the diff found was reported
        }
    }

    [Fact]
    public void FeedMatchesTheDiffExactly_WhenNoWriteIsRedundant()
    {
        // Same oracle, tightened: every mutation here genuinely changes something, so over-reporting
        // has nowhere to hide and the two sets must be equal, not merely nested.
        foreach (int seed in (int[])[5, 23, 61])
        {
            var kernel = new HypergraphKernel();
            kernel.EnableChangeLog(8192);
            var intKey = kernel.ResolveProperty<int>("i");
            var strKey = kernel.ResolveProperty<string>("s");

            var handles = new List<Handle> { kernel.CreateVertex(), kernel.CreateVertex(VertexRoles.Edge) };
            var before = Capture(kernel, intKey, strKey);
            long cursor = kernel.CurrentRevision;

            var rng = new Random(seed);
            var dormant = new HashSet<Handle>();
            for (int i = 0; i < 250; i++)
            {
                var pick = handles[rng.Next(handles.Count)];
                switch (rng.Next(5))
                {
                    case 0: handles.Add(kernel.CreateVertex()); break;
                    case 1: kernel.AddIncidence(handles[rng.Next(handles.Count)], pick, (byte)rng.Next(3), i); break;
                    case 2: kernel.SetProperty(intKey, pick, i); break;          // i is never repeated
                    case 3: kernel.SetProperty(strKey, pick, $"v{i}"); break;    // nor is this
                    default:
                        if (dormant.Add(pick)) kernel.SetDormant(pick);          // only ever a real flip
                        break;
                }
            }

            var after = Capture(kernel, intKey, strKey);
            var expected = Diff(before, after);
            var reported = kernel.ChangesSince(cursor, out bool overflowed).Select(c => c.Handle).ToHashSet();

            Assert.False(overflowed);
            Assert.Equal(expected, reported);
        }
    }

    [Fact]
    public void AnUnchangedKernelReportsNothing()
    {
        var kernel = new HypergraphKernel();
        kernel.EnableChangeLog(64);
        var intKey = kernel.ResolveProperty<int>("i");
        var strKey = kernel.ResolveProperty<string>("s");
        kernel.CreateVertex();

        var before = Capture(kernel, intKey, strKey);
        long cursor = kernel.CurrentRevision;

        // Reads only.
        _ = ((IHypergraphQuery)kernel).GetConnectedComponents();
        _ = kernel.VertexHandles();

        var after = Capture(kernel, intKey, strKey);
        Assert.Empty(Diff(before, after));
        Assert.Empty(kernel.ChangesSince(cursor, out _));
    }
}
