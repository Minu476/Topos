using System.Collections.Concurrent;

namespace Topos.Hypergraph;

/// <summary>
/// Per-process string-to-int property registry (spec §3: "identity (string) separate from
/// PropertyId (int, per-process registry)"). Thread-safe; an Id is assigned once per name and
/// never reused, so a <see cref="PropertyKey{T}"/> resolved early in the process stays valid for
/// its lifetime.
///
/// Note: the name-to-id mapping is untyped (a name maps to exactly one Id regardless of T).
/// Resolving the same name with two different T's is a caller error — it yields two
/// <see cref="PropertyKey{T}"/> values sharing an Id but backed by differently-typed pools in
/// <see cref="HypergraphKernel"/>, which will throw an <see cref="InvalidCastException"/> on
/// first access. This is intentionally not guarded against in M0 (single-writer discipline is
/// the consumer's responsibility); revisit if this proves error-prone in practice.
/// </summary>
public sealed class PropertyRegistry
{
    private readonly ConcurrentDictionary<string, int> _ids = new(StringComparer.Ordinal);
    private int _next = -1;

    public PropertyKey<T> Resolve<T>(string name)
    {
        int id = _ids.GetOrAdd(name, static (_, self) => Interlocked.Increment(ref self._next), this);
        return new PropertyKey<T>(name, id);
    }
}
