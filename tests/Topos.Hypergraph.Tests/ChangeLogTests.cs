using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

/// <summary>
/// M13 §5.2 — T4 hand-computed cases for the change log: small sequences whose expected output is
/// derived from the definition rather than from running the code.
/// </summary>
public class ChangeLogTests
{
    private static HypergraphKernel Logged(int capacity = 64)
    {
        var kernel = new HypergraphKernel();
        kernel.EnableChangeLog(capacity);
        return kernel;
    }

    [Fact]
    public void OffByDefault_AndSaysSoClearly()
    {
        var kernel = new HypergraphKernel();
        Assert.False(kernel.IsChangeLogEnabled);

        var ex = Assert.Throws<InvalidOperationException>(() => kernel.ChangesSince(0, out _));
        Assert.Contains("EnableChangeLog", ex.Message);

        // The counter works regardless -- that is the §6.1 split.
        kernel.CreateVertex();
        Assert.Equal(1, kernel.CurrentRevision);
    }

    [Fact]
    public void ReportsEachTouchedHandleOnceWithItsKind()
    {
        var kernel = Logged();
        var a = kernel.CreateVertex();

        var changes = kernel.ChangesSince(0, out bool overflowed);

        Assert.False(overflowed);
        var only = Assert.Single(changes);
        Assert.Equal(a, only.Handle);
        Assert.Equal(ChangeKind.VertexCreated, only.Kind);
        Assert.Equal(1, only.Revision);
    }

    [Fact]
    public void CoalescesRepeatedWritesToOneHandle_UnioningTheKinds()
    {
        var kernel = Logged();
        var key = kernel.ResolveProperty<int>("k");

        var a = kernel.CreateVertex();   // VertexCreated
        kernel.SetProperty(key, a, 1);   // PropertySet
        kernel.SetProperty(key, a, 2);   // PropertySet again -- still one entry
        kernel.SetDormant(a);            // StatusChanged

        var only = Assert.Single(kernel.ChangesSince(0, out _));
        Assert.Equal(a, only.Handle);
        Assert.Equal(
            ChangeKind.VertexCreated | ChangeKind.PropertySet | ChangeKind.StatusChanged,
            only.Kind);
        Assert.Equal(kernel.CurrentRevision, only.Revision); // carries the newest revision
    }

    [Fact]
    public void AddIncidence_ReportsBothParticipants()
    {
        // A consumer watching only the member would otherwise never learn it joined a relation.
        var kernel = Logged();
        var member = kernel.CreateVertex();
        var edge = kernel.CreateVertex(VertexRoles.Edge);

        long before = kernel.CurrentRevision;
        kernel.AddIncidence(edge, member, role: 1, ordinal: 0);

        var changes = kernel.ChangesSince(before, out _);
        Assert.Equal(2, changes.Count);
        // HasFlag, not equality: an entry's Kind accumulates over its whole life in the ring, not
        // just since this cursor, so these two still carry the VertexCreated they were born with.
        // That is the documented over-report -- a consumer re-reads something it already had.
        Assert.Contains(changes, c => c.Handle == edge && c.Kind.HasFlag(ChangeKind.IncidenceAdded));
        Assert.Contains(changes, c => c.Handle == member && c.Kind.HasFlag(ChangeKind.IncidenceAdded));

        // One write, one revision -- both entries carry it.
        Assert.All(changes, c => Assert.Equal(before + 1, c.Revision));
    }

    [Fact]
    public void SelfIncidence_ReportsOneEntryNotTwo()
    {
        var kernel = Logged();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        long before = kernel.CurrentRevision;

        kernel.AddIncidence(edge, edge, role: 0, ordinal: 0);

        Assert.Single(kernel.ChangesSince(before, out _));
    }

    [Fact]
    public void OrdersOldestFirst()
    {
        var kernel = Logged();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        var c = kernel.CreateVertex();

        var revisions = kernel.ChangesSince(0, out _).Select(x => x.Revision).ToList();

        Assert.Equal([1L, 2L, 3L], revisions);
        Assert.Equal([a, b, c], kernel.ChangesSince(0, out _).Select(x => x.Handle));
    }

    [Fact]
    public void CoalescingMovesAnEntryToTheEnd()
    {
        // Re-touching a handle re-dates it, so "oldest first" stays true of the *latest* write to
        // each handle -- which is the ordering a consumer replaying the list needs.
        var kernel = Logged();
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();
        kernel.SetDormant(a);

        Assert.Equal([b, a], kernel.ChangesSince(0, out _).Select(x => x.Handle));
    }

    [Fact]
    public void EmptyAtTheHead()
    {
        var kernel = Logged();
        kernel.CreateVertex();

        Assert.Empty(kernel.ChangesSince(kernel.CurrentRevision, out bool overflowed));
        Assert.False(overflowed);
    }

    [Fact]
    public void Overflow_IsReportedNotSwallowed()
    {
        var kernel = Logged(capacity: 4);
        for (int i = 0; i < 10; i++) kernel.CreateVertex();

        var changes = kernel.ChangesSince(0, out bool overflowed);

        Assert.True(overflowed);                 // the cursor at 0 is long gone
        Assert.Equal(4, changes.Count);          // a suffix, and the ring says so
        Assert.Equal([7L, 8L, 9L, 10L], changes.Select(c => c.Revision));
    }

    [Fact]
    public void ARecentCursorDoesNotOverflow_EvenOnASmallRing()
    {
        var kernel = Logged(capacity: 4);
        for (int i = 0; i < 10; i++) kernel.CreateVertex();

        long cursor = kernel.CurrentRevision;
        kernel.CreateVertex();
        kernel.CreateVertex();

        var changes = kernel.ChangesSince(cursor, out bool overflowed);
        Assert.False(overflowed);
        Assert.Equal(2, changes.Count);
    }

    [Fact]
    public void CapacityIsSpentOnDistinctHandles_NotOnWriteVolume()
    {
        // The whole argument for coalescing: a hot vertex cannot flush the ring.
        var kernel = Logged(capacity: 4);
        var key = kernel.ResolveProperty<int>("k");
        var a = kernel.CreateVertex();
        var b = kernel.CreateVertex();

        for (int i = 0; i < 1000; i++) kernel.SetProperty(key, a, i);

        var changes = kernel.ChangesSince(0, out bool overflowed);
        Assert.False(overflowed);       // b was never evicted despite 1000 writes
        Assert.Equal(2, changes.Count);
        Assert.Contains(changes, c => c.Handle == b);
    }

    [Fact]
    public void EnablingAgain_ResetsTheLogAndSaysSoToOldCursors()
    {
        var kernel = Logged();
        kernel.CreateVertex();
        long cursor = kernel.CurrentRevision;
        kernel.CreateVertex();

        kernel.EnableChangeLog(8);

        Assert.Empty(kernel.ChangesSince(cursor, out bool overflowed));
        Assert.False(overflowed);   // an empty ring has evicted nothing; the revision still moved
        Assert.True(kernel.CurrentRevision >= cursor);
    }

    [Fact]
    public void Disable_StopsRetainingButNotCounting()
    {
        var kernel = Logged();
        kernel.DisableChangeLog();

        Assert.False(kernel.IsChangeLogEnabled);
        kernel.CreateVertex();
        Assert.Equal(1, kernel.CurrentRevision);
        Assert.Throws<InvalidOperationException>(() => kernel.ChangesSince(0, out _));
    }

    [Fact]
    public void CapacityBelowOne_ThrowsImmediately()
    {
        var kernel = new HypergraphKernel();
        Assert.Throws<ArgumentOutOfRangeException>(() => kernel.EnableChangeLog(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => kernel.EnableChangeLog(-1));
    }
}
