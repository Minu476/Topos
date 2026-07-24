using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

/// <summary>M3: set algebra (spec §6) -- Intersect/Difference over IHypergraphQuery, and the "doubling as version-diff" application.</summary>
public class SetAlgebraTests
{
    [Fact]
    public void Intersect_OnlyVerticesInBoth()
    {
        var raw = new HypergraphKernel();
        var a = raw.CreateVertex();
        var b = raw.CreateVertex();
        var c = raw.CreateVertex();

        var viewAB = HypergraphViews.Subgraph(raw, h => h == a || h == b);
        var viewBC = HypergraphViews.Subgraph(raw, h => h == b || h == c);
        var intersection = HypergraphViews.Intersect(viewAB, viewBC);

        Assert.Equal(1, intersection.CountVertices());
        Assert.True(intersection.ContainsVertex(b));
        Assert.False(intersection.ContainsVertex(a));
        Assert.False(intersection.ContainsVertex(c));
    }

    [Fact]
    public void Intersect_Disjoint_IsEmpty()
    {
        var raw = new HypergraphKernel();
        var a = raw.CreateVertex();
        var b = raw.CreateVertex();

        var viewA = HypergraphViews.Subgraph(raw, h => h == a);
        var viewB = HypergraphViews.Subgraph(raw, h => h == b);

        Assert.Equal(0, HypergraphViews.Intersect(viewA, viewB).CountVertices());
    }

    [Fact]
    public void Difference_VerticesInAButNotB()
    {
        var raw = new HypergraphKernel();
        var a = raw.CreateVertex();
        var b = raw.CreateVertex();
        var c = raw.CreateVertex();

        var viewABC = HypergraphViews.Subgraph(raw, h => h == a || h == b || h == c);
        var viewB = HypergraphViews.Subgraph(raw, h => h == b);
        var diff = HypergraphViews.Difference(viewABC, viewB);

        Assert.Equal(2, diff.CountVertices());
        Assert.True(diff.ContainsVertex(a));
        Assert.False(diff.ContainsVertex(b));
        Assert.True(diff.ContainsVertex(c));
    }

    [Fact]
    public void VersionDiff_UsingMonotonicHandleIndexThresholds_FindsWhatWasAddedSince()
    {
        // The worked example from HypergraphViews' class doc: because Handle.Index is monotonic
        // and never reused within one kernel's lifetime, an index-threshold Subgraph is a real
        // "state as of an earlier point" snapshot, not a coincidence -- so Difference over two
        // such thresholds is a genuine version-diff, with no persistence/snapshotting layer
        // needed (that's M4's job for cross-session versioning; this covers within-one-kernel
        // history today).
        var kernel = new HypergraphKernel();

        kernel.CreateVertex(); // v0
        kernel.CreateVertex(); // v1
        kernel.CreateVertex(); // v2
        uint snapshotAThreshold = 3; // "as of here" -- v0, v1, v2 exist

        var v3 = kernel.CreateVertex();
        var v4 = kernel.CreateVertex();
        uint snapshotBThreshold = 5; // "as of here" -- v0..v4 exist

        var snapshotA = HypergraphViews.Subgraph(kernel, h => h.Index < snapshotAThreshold);
        var snapshotB = HypergraphViews.Subgraph(kernel, h => h.Index < snapshotBThreshold);

        var addedSinceA = HypergraphViews.Difference(snapshotB, snapshotA);

        Assert.Equal(2, addedSinceA.CountVertices());
        Assert.True(addedSinceA.ContainsVertex(v3));
        Assert.True(addedSinceA.ContainsVertex(v4));
        Assert.Equal(3, snapshotA.CountVertices());
        Assert.Equal(5, snapshotB.CountVertices());
    }

    [Fact]
    public void VersionDiff_NoChangeBetweenSnapshots_DifferenceIsEmpty()
    {
        var kernel = new HypergraphKernel();
        kernel.CreateVertex();
        kernel.CreateVertex();
        uint threshold = 2;

        var snapshotA = HypergraphViews.Subgraph(kernel, h => h.Index < threshold);
        var snapshotB = HypergraphViews.Subgraph(kernel, h => h.Index < threshold);

        Assert.Equal(0, HypergraphViews.Difference(snapshotB, snapshotA).CountVertices());
    }
}
