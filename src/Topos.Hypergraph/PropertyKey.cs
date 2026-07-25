namespace Topos.Hypergraph;

/// <summary>
/// A typed property identity (spec §3, primitive 4). <see cref="Name"/> is the stable,
/// human-facing identity; <see cref="Id"/> is the per-process registry slot resolved once via
/// <see cref="PropertyRegistry"/> and cached here for O(1) pool lookup.
///
/// <b>The constructor is internal, not public — locked M8.</b> The only legitimate way to obtain
/// a <see cref="PropertyKey{T}"/> is <see cref="PropertyRegistry.Resolve{T}"/>. A public
/// constructor would let a consumer construct two keys sharing an <see cref="Id"/> but backed by
/// different <typeparamref name="T"/>s (e.g. <c>new PropertyKey&lt;int&gt;("foo", 5)</c> and
/// <c>new PropertyKey&lt;string&gt;("foo", 5)</c>), which throws <see cref="InvalidCastException"/>
/// on first pool access (see <see cref="PropertyRegistry"/>'s doc) — a footgun with no legitimate
/// use, closed before any external consumer could depend on constructing one directly.
/// </summary>
public readonly record struct PropertyKey<T>
{
    internal PropertyKey(string name, int id)
    {
        Name = name;
        Id = id;
    }

    public string Name { get; }
    public int Id { get; }
}
