namespace Topos.Hypergraph;

/// <summary>
/// A typed property identity (spec §3, primitive 4). <see cref="Name"/> is the stable,
/// human-facing identity; <see cref="Id"/> is the per-process registry slot resolved once via
/// <see cref="PropertyRegistry"/> and cached here for O(1) pool lookup.
/// </summary>
public readonly record struct PropertyKey<T>(string Name, int Id);
