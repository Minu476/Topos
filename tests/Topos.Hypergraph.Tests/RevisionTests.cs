using Topos.Hypergraph;

namespace Topos.Hypergraph.Tests;

/// <summary>
/// M13 §5.1 — the revision counter. Table-driven over every write method on the kernel, because the
/// failure mode this guards against is not "the counter is wrong" but "someone added an eighth
/// write method and forgot to hook it."
/// </summary>
public class RevisionTests
{
    [Fact]
    public void StartsAtZero()
    {
        Assert.Equal(0, new HypergraphKernel().CurrentRevision);
    }

    [Fact]
    public void EveryWriteMethodBumpsExactlyOnce()
    {
        var kernel = new HypergraphKernel();
        var key = kernel.ResolveProperty<int>("k");

        var a = kernel.CreateVertex();
        var edge = kernel.CreateVertex(VertexRoles.Edge);

        (string Name, Action Write)[] writes =
        [
            ("CreateVertex",   () => kernel.CreateVertex()),
            ("AddIncidence",   () => kernel.AddIncidence(edge, a, 0, 0)),
            ("SetProperty",    () => kernel.SetProperty(key, a, 1)),
            ("SetDormant",     () => kernel.SetDormant(a)),
            ("Reactivate",     () => kernel.Reactivate(a)),
            ("RemoveProperty", () => kernel.RemoveProperty(key, a)),
        ];

        foreach (var (name, write) in writes)
        {
            long before = kernel.CurrentRevision;
            write();
            Assert.Equal($"{name}:{before + 1}", $"{name}:{kernel.CurrentRevision}");
        }
    }

    [Fact]
    public void ReadsNeverBump()
    {
        var kernel = new HypergraphKernel();
        var key = kernel.ResolveProperty<int>("k");
        var a = kernel.CreateVertex();
        var edge = kernel.CreateVertex(VertexRoles.Edge);
        kernel.AddIncidence(edge, a, 0, 0);
        kernel.SetProperty(key, a, 7);

        long before = kernel.CurrentRevision;

        kernel.TryGetVertex(a, out _);
        kernel.TryGetProperty(key, a, out _);
        kernel.IncidencesFrom(edge);
        kernel.IncidencesOf(a);
        kernel.VertexHandles();
        kernel.CountVertices();
        _ = ((IHypergraphQuery)kernel).GetConnectedComponents();
        _ = ((IHypergraphQuery)kernel).GetSConnectedComponents();

        Assert.Equal(before, kernel.CurrentRevision);
    }

    [Fact]
    public void WritesTheKernelSeesAsNoOps_DoNotBump()
    {
        // Documented in CurrentRevision's own doc: the counter may over-report, but it never counts
        // a no-op it can detect for free.
        var kernel = new HypergraphKernel();
        var key = kernel.ResolveProperty<int>("k");
        var a = kernel.CreateVertex();

        kernel.SetDormant(a);
        long before = kernel.CurrentRevision;

        kernel.SetDormant(a);                       // already dormant
        kernel.RemoveProperty(key, a);              // nothing to remove
        kernel.RemoveProperty(key, Handle.Invalid); // never had anything

        Assert.Equal(before, kernel.CurrentRevision);
    }

    [Fact]
    public void SetProperty_BumpsEvenWhenTheValueIsUnchanged()
    {
        // The deliberate over-report, asserted so it is a documented contract rather than an
        // accident: detecting it would cost an equality comparison on every property write, and
        // over-reporting only ever costs a consumer a redundant re-read.
        var kernel = new HypergraphKernel();
        var key = kernel.ResolveProperty<int>("k");
        var a = kernel.CreateVertex();
        kernel.SetProperty(key, a, 5);

        long before = kernel.CurrentRevision;
        kernel.SetProperty(key, a, 5);

        Assert.Equal(before + 1, kernel.CurrentRevision);
    }

    [Fact]
    public void RestoreVertex_DoesNotBump()
    {
        // §6.6: a reload is a new baseline, not a change.
        var kernel = new HypergraphKernel(startingHandleIndex: 10);
        long before = kernel.CurrentRevision;

        kernel.RestoreVertex(new Handle(3), VertexRoles.None, VertexStatus.Active);
        kernel.RestoreVertex(new Handle(4), VertexRoles.Edge, VertexStatus.Dormant);

        Assert.Equal(before, kernel.CurrentRevision);
    }

    [Fact]
    public void SnapshotReloadLeavesAFreshBaseline()
    {
        // The consequence of the above that actually matters to a consumer: a reloaded kernel does
        // not claim a hundred thousand things just changed.
        var kernel = new HypergraphKernel(startingHandleIndex: 100);
        for (uint i = 0; i < 50; i++)
            kernel.RestoreVertex(new Handle(i), VertexRoles.None, VertexStatus.Active);

        Assert.Equal(0, kernel.CurrentRevision);
    }

    [Fact]
    public void IsMonotonicAcrossAMixedSequence()
    {
        var kernel = new HypergraphKernel();
        var key = kernel.ResolveProperty<string>("s");
        var rng = new Random(4242);
        var handles = new List<Handle> { kernel.CreateVertex() };

        long previous = kernel.CurrentRevision;
        for (int i = 0; i < 300; i++)
        {
            switch (rng.Next(4))
            {
                case 0: handles.Add(kernel.CreateVertex()); break;
                case 1: kernel.SetProperty(key, handles[rng.Next(handles.Count)], $"v{i}"); break;
                case 2: kernel.SetDormant(handles[rng.Next(handles.Count)]); break;
                default: kernel.Reactivate(handles[rng.Next(handles.Count)]); break;
            }
            Assert.True(kernel.CurrentRevision >= previous);
            previous = kernel.CurrentRevision;
        }
    }
}
