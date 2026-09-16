using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

/// <summary>
/// M13 §3.2 — the metamorphic (T3) properties. Each holds for <i>any</i> correct change feed, with
/// no reference implementation to compare against, which is what makes them the right tool here:
/// `LIVING_GRAPH_SPEC.md` §3 has no external oracle to reach for.
///
/// All eight run against the same seeded random mutation sequences, so a failure is reproducible
/// from the seed alone.
/// </summary>
public class ChangeLogPropertyTests
{
    private static readonly int[] Seeds = [1, 7, 13, 29, 101];

    /// <summary>
    /// A seeded mutation sequence over a small handle pool, deliberately mixing every write method
    /// including the ones the kernel treats as no-ops.
    /// </summary>
    private static List<Handle> Mutate(HypergraphKernel kernel, int seed, int steps)
    {
        var rng = new Random(seed);
        var intKey = kernel.ResolveProperty<int>("i");
        var strKey = kernel.ResolveProperty<string>("s");
        var handles = new List<Handle> { kernel.CreateVertex() };

        for (int i = 0; i < steps; i++)
        {
            var pick = handles[rng.Next(handles.Count)];
            switch (rng.Next(7))
            {
                case 0: handles.Add(kernel.CreateVertex()); break;
                case 1: handles.Add(kernel.CreateVertex(VertexRoles.Edge)); break;
                case 2: kernel.AddIncidence(handles[rng.Next(handles.Count)], pick, (byte)rng.Next(4), i); break;
                case 3: kernel.SetProperty(intKey, pick, i); break;
                case 4: kernel.SetProperty(strKey, pick, $"v{i}"); break;
                case 5: kernel.SetDormant(pick); break;
                default: kernel.Reactivate(pick); break;
            }
        }
        return handles;
    }

    [Fact]
    public void Property1_RevisionNeverDecreases()
    {
        foreach (int seed in Seeds)
        {
            var kernel = new HypergraphKernel();
            kernel.EnableChangeLog(4096);
            long previous = 0;
            var rng = new Random(seed);
            var handles = new List<Handle> { kernel.CreateVertex() };
            for (int i = 0; i < 200; i++)
            {
                if (rng.Next(2) == 0) handles.Add(kernel.CreateVertex());
                else kernel.SetDormant(handles[rng.Next(handles.Count)]);
                Assert.True(kernel.CurrentRevision >= previous);
                previous = kernel.CurrentRevision;
            }
        }
    }

    [Fact]
    public void Property4_EmptyAtTheHead()
    {
        foreach (int seed in Seeds)
        {
            var kernel = new HypergraphKernel();
            kernel.EnableChangeLog(4096);
            Mutate(kernel, seed, 200);

            Assert.Empty(kernel.ChangesSince(kernel.CurrentRevision, out bool overflowed));
            Assert.False(overflowed);
        }
    }

    [Fact]
    public void Property5_ChangesSinceZeroCoversEveryHandleAFullScanFinds()
    {
        foreach (int seed in Seeds)
        {
            var kernel = new HypergraphKernel();
            kernel.EnableChangeLog(4096);   // capacity above the distinct handles these produce
            Mutate(kernel, seed, 200);

            var reported = kernel.ChangesSince(0, out bool overflowed).Select(c => c.Handle).ToHashSet();
            Assert.False(overflowed);
            Assert.Equal(kernel.VertexHandles().ToHashSet(), reported);
        }
    }

    [Fact]
    public void Property6_ANewerCursorSeesASubsetOfAnOlderOne()
    {
        foreach (int seed in Seeds)
        {
            var kernel = new HypergraphKernel();
            kernel.EnableChangeLog(4096);
            Mutate(kernel, seed, 120);
            long middle = kernel.CurrentRevision;
            Mutate(kernel, seed + 1000, 120);

            var older = kernel.ChangesSince(0, out _).Select(c => c.Handle).ToHashSet();
            var newer = kernel.ChangesSince(middle, out _).Select(c => c.Handle).ToHashSet();

            Assert.Subset(older, newer);
        }
    }

    [Fact]
    public void Property7_EachHandleAppearsAtMostOnce()
    {
        foreach (int seed in Seeds)
        {
            var kernel = new HypergraphKernel();
            kernel.EnableChangeLog(4096);
            Mutate(kernel, seed, 200);

            var changes = kernel.ChangesSince(0, out _);
            Assert.Equal(changes.Count, changes.Select(c => c.Handle).Distinct().Count());
        }
    }

    [Fact]
    public void ResultIsOrderedOldestFirst()
    {
        foreach (int seed in Seeds)
        {
            var kernel = new HypergraphKernel();
            kernel.EnableChangeLog(4096);
            Mutate(kernel, seed, 200);

            var revisions = kernel.ChangesSince(0, out _).Select(c => c.Revision).ToList();
            Assert.Equal(revisions.OrderBy(r => r), revisions);
        }
    }

    [Fact]
    public void EveryReportedRevisionIsWithinRange()
    {
        foreach (int seed in Seeds)
        {
            var kernel = new HypergraphKernel();
            kernel.EnableChangeLog(4096);
            Mutate(kernel, seed, 200);

            foreach (var change in kernel.ChangesSince(0, out _))
            {
                Assert.InRange(change.Revision, 1, kernel.CurrentRevision);
            }
        }
    }

    [Fact]
    public void Property8_OverflowIsSignalledWheneverAnythingWasDropped()
    {
        // The property that matters most: a reader that fell behind must be TOLD. Asserted as an
        // implication -- if the feed did not report everything a full scan finds, it must have said
        // it overflowed. A feed that silently truncates fails here.
        foreach (int seed in Seeds)
        {
            var kernel = new HypergraphKernel();
            kernel.EnableChangeLog(8);      // deliberately far too small
            Mutate(kernel, seed, 200);

            var reported = kernel.ChangesSince(0, out bool overflowed).Select(c => c.Handle).ToHashSet();
            bool complete = kernel.VertexHandles().All(reported.Contains);

            Assert.True(complete || overflowed,
                "the feed returned an incomplete answer without signalling overflow");
        }
    }

    [Fact]
    public void Property3_NoOpWritesLeaveBothTheRevisionAndTheLogAlone()
    {
        var kernel = new HypergraphKernel();
        kernel.EnableChangeLog(64);
        var key = kernel.ResolveProperty<int>("k");
        var a = kernel.CreateVertex();
        kernel.SetDormant(a);

        long cursor = kernel.CurrentRevision;
        kernel.SetDormant(a);
        kernel.RemoveProperty(key, a);

        Assert.Equal(cursor, kernel.CurrentRevision);
        Assert.Empty(kernel.ChangesSince(cursor, out _));
    }

    [Fact]
    public void Property2_TheLogNeverRunsAheadOfTheRevision()
    {
        // The publish-ordering invariant, stated as a property: every recorded revision must be one
        // the counter has already reached.
        foreach (int seed in Seeds)
        {
            var kernel = new HypergraphKernel();
            kernel.EnableChangeLog(4096);
            Mutate(kernel, seed, 200);

            foreach (var change in kernel.ChangesSince(0, out _))
            {
                Assert.True(change.Revision <= kernel.CurrentRevision);
            }
        }
    }
}
