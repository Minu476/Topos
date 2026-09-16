using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

/// <summary>
/// M13 §3.3 — the single-writer/multi-reader stress test.
///
/// This is the first Topos feature whose correctness depends on the threading model rather than
/// merely respecting it, and a concurrency test that can never fail is indistinguishable from one
/// that passes. So these assert a specific, breakable invariant rather than "nothing threw":
///
/// <b>A reader that observes <see cref="HypergraphKernel.CurrentRevision"/> = r must find the log
/// already holding r.</b> That is exactly what <c>RecordChange</c>'s ordering buys — record into the
/// log first, publish the revision second. Reverse those two lines and
/// <see cref="ReadersNeverSeeARevisionTheLogHasNotRecorded"/> fails within a few thousand writes;
/// that was verified by actually reversing them, not by inspection.
/// </summary>
public class ChangeLogConcurrencyTests
{
    private const int Writes = 20_000;
    private const int Readers = 4;

    [Fact]
    public async Task ReadersNeverSeeARevisionTheLogHasNotRecorded()
    {
        var kernel = new HypergraphKernel();
        kernel.EnableChangeLog(capacity: 64_000);
        var key = kernel.ResolveProperty<int>("k");

        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
        using var done = new CancellationTokenSource();

        var readers = Enumerable.Range(0, Readers).Select(reader => Task.Run(() =>
        {
            while (!done.IsCancellationRequested)
            {
                long observed = kernel.CurrentRevision;
                var changes = kernel.ChangesSince(0, out bool overflowed);
                if (overflowed || observed == 0) continue;

                long highestLogged = changes.Count == 0 ? 0 : changes[^1].Revision;
                if (highestLogged < observed)
                {
                    failures.Add($"observed revision {observed} but the log's newest entry was {highestLogged}");
                }
            }
        })).ToArray();

        var handles = new List<Handle> { kernel.CreateVertex() };
        for (int i = 0; i < Writes; i++)
        {
            if (i % 50 == 0) handles.Add(kernel.CreateVertex());
            kernel.SetProperty(key, handles[i % handles.Count], i);
        }

        done.Cancel();
        await Task.WhenAll(readers);

        Assert.Empty(failures);
    }

    [Fact]
    public async Task ConcurrentReadsNeverTearOrThrow()
    {
        // The lock's job, asserted through an invariant that tearing must break.
        //
        // GraphChange is 16+ bytes -- a long, a Handle, an enum -- so it is not written atomically.
        // A reader racing a slot rewrite can observe one field from the old entry and another from
        // the new one. The trick is making that detectable: this writer does nothing but
        // CreateVertex, so handle #i is always created at revision i+1, and every honest entry
        // satisfies Revision == Handle.Index + 1. A torn read pairs a fresh revision with a stale
        // handle and breaks it immediately.
        //
        // The first version of this test asserted only "Revision > 0 && Kind != None" and passed
        // happily with every lock removed -- the exact can-never-fail concurrency test that is
        // indistinguishable from a real one. Removing the locks now fails it within milliseconds,
        // which was verified by actually removing them.
        var kernel = new HypergraphKernel();
        kernel.EnableChangeLog(capacity: 256);   // small, so slots churn constantly

        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
        using var done = new CancellationTokenSource();

        var readers = Enumerable.Range(0, Readers).Select(reader => Task.Run(() =>
        {
            try
            {
                while (!done.IsCancellationRequested)
                {
                    foreach (var change in kernel.ChangesSince(0, out _))
                    {
                        if (change.Revision != change.Handle.Index + 1)
                            failures.Add($"torn entry: {change} (expected revision {change.Handle.Index + 1})");
                        if (change.Kind != ChangeKind.VertexCreated)
                            failures.Add($"torn kind: {change}");
                    }
                }
            }
            catch (Exception ex) { failures.Add($"{ex.GetType().Name}: {ex.Message}"); }
        })).ToArray();

        for (int i = 0; i < Writes; i++) kernel.CreateVertex();

        done.Cancel();
        await Task.WhenAll(readers);

        Assert.Empty(failures);
    }

    [Fact]
    public async Task EveryWrittenHandleIsReportedOnceTheWriterFinishes()
    {
        // Completeness under concurrency: readers running throughout must not cost the final reader
        // anything -- the feed is not consumed by being read.
        var kernel = new HypergraphKernel();
        kernel.EnableChangeLog(capacity: 64_000);
        var key = kernel.ResolveProperty<int>("k");

        using var done = new CancellationTokenSource();
        var readers = Enumerable.Range(0, Readers).Select(reader => Task.Run(() =>
        {
            while (!done.IsCancellationRequested) _ = kernel.ChangesSince(0, out _);
        })).ToArray();

        var written = new HashSet<Handle>();
        for (int i = 0; i < 5_000; i++)
        {
            var h = kernel.CreateVertex();
            written.Add(h);
            kernel.SetProperty(key, h, i);
        }

        done.Cancel();
        await Task.WhenAll(readers);

        var reported = kernel.ChangesSince(0, out bool overflowed).Select(c => c.Handle).ToHashSet();
        Assert.False(overflowed);
        Assert.Subset(reported, written);
    }
}
