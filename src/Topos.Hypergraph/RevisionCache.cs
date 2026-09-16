namespace Topos.Hypergraph;

/// <summary>
/// A derived value that recomputes only when the graph actually moved (M13, `docs/LIVING_GRAPH_SPEC.md`
/// §2 item 6). The consumer-side payoff of <see cref="HypergraphKernel.CurrentRevision"/>, and the
/// shape most consumers want before they want the change feed itself.
///
/// The problem it solves is the one M13 exists for. Topos's views are live — a
/// <see cref="HypergraphViews.Mask"/> predicate re-evaluates on every call — but "live" means
/// *recomputed*, so anything derived from the whole graph (a PageRank ranking, a component
/// partition, a filtered handle list) costs O(V) every time a caller asks, whether or not anything
/// changed. Before M13 there was no way to tell the difference. Now there is, and it is an integer
/// compare:
///
/// <code>
/// var ranking = new RevisionCache&lt;IReadOnlyDictionary&lt;Handle, double&gt;&gt;(kernel, k =&gt; PageRank.Compute(k));
/// var today = ranking.Value;     // computes
/// var again = ranking.Value;     // free — nothing was written in between
/// kernel.CreateVertex();
/// var fresh = ranking.Value;     // recomputes, because the graph moved
/// </code>
///
/// <b>Deliberately coarse.</b> Any write to any handle invalidates the whole cached value, because
/// <see cref="HypergraphKernel.CurrentRevision"/> is one number for the whole kernel. For a
/// whole-graph derivation like PageRank that is exactly right — any write can change every score.
/// For a value that depends on a small subset, this will recompute more often than strictly
/// necessary; that consumer wants <see cref="HypergraphKernel.ChangesSince"/> and its own
/// invalidation rule, not this.
///
/// <b>Not thread-safe</b>, by design rather than by omission: it holds one consumer's cursor, and
/// two threads sharing one cursor is a consumer-side race no amount of locking here would make
/// meaningful. Give each reader its own — they are cheap, and the kernel's SWMR model already
/// assumes concurrent readers hold their own state.
/// </summary>
public sealed class RevisionCache<T>(HypergraphKernel kernel, Func<HypergraphKernel, T> compute)
{
    private long _computedAt = -1;
    private T? _value;

    /// <summary>
    /// The derived value, recomputed only if the kernel has been written to since the last read.
    /// </summary>
    public T Value
    {
        get
        {
            long now = kernel.CurrentRevision;
            if (now != _computedAt)
            {
                _value = compute(kernel);
                _computedAt = now;
                Recomputations++;
            }
            return _value!;
        }
    }

    /// <summary>
    /// How many times the value has actually been computed. Exposed because "did my cache work?" is
    /// otherwise unobservable — it is what lets a test assert that a read was free rather than
    /// merely fast, and what lets a consumer confirm its invalidation assumptions against a real
    /// workload instead of hoping.
    /// </summary>
    public int Recomputations { get; private set; }

    /// <summary>The revision the cached value was computed at, or -1 before the first read.</summary>
    public long ComputedAtRevision => _computedAt;

    /// <summary>Drops the cached value so the next read recomputes, regardless of the revision.</summary>
    public void Invalidate() => _computedAt = -1;
}
