# Role conventions — a layer-1 pattern, not a kernel feature

**Date:** 2026-07-25 · **Author:** Opus 5 (M8 API-stability session) · **Status:** Settled M8,
see `docs/DECISIONS.md`. Resolves finding #3 of `docs/NEXUS_VERIFIER_INTEGRATION_FINDINGS.md`.

## The problem

`HypergraphKernel.AddIncidence(Handle source, Handle member, byte role, int ordinal)` takes
`role` as a raw `byte`. This is correct and staying as-is — `Incidence.cs`'s doc is explicit that
"the kernel records; it does not judge" (spec §4.1) — but it means every consumer independently
reinvents the same boilerplate:

```csharp
// samples/Topos.Samples.ChatMemory/ChatMemory.cs
private const byte SpeakerRole = 0, MentionedRole = 1, DerivedFromRole = 1, ...;

// NexusVerifier's adapter (Topos.Hypergraph.Knowledge integration findings, finding #3)
public const byte BeforeRole = 0;  // V2 "Anchor"
public const byte AfterRole  = 1;  // V2 "Condition"
```

No compile-time help, no registry, no way to detect a collision if two consumers ever share one
kernel instance.

## The decision

**M8 does not change the kernel.** `AddIncidence` keeps taking a raw `byte`; there is no
`AddIncidence<TRole>` generic overload and no kernel-level role registry. Adding either would put
domain judgment (what a role byte *means*) into the one place the project has consistently kept
it out of.

Instead, **consumers should follow this documented convention**, not invent their own each time:

```csharp
// Define role bytes as a plain byte enum, scoped to your domain, not the kernel:
public enum ChainerRole : byte
{
    Before = 0,   // maps to V2 "Anchor"
    After  = 1,   // maps to V2 "Condition"
}

// At the AddIncidence call site, cast explicitly — the kernel still only ever sees a byte:
kernel.AddIncidence(source, member, (byte)ChainerRole.Before, ordinal: 0);

// On read, cast back for role-filtered traversal (the pattern both ChatMemory and
// NexusVerifier's n-ary chainer already hand-roll):
var afterMembers = kernel.IncidencesFrom(source)
    .Where(i => (ChainerRole)i.Role == ChainerRole.After);
```

This gets you compile-time-checked, named roles with zero kernel API surface and zero runtime
cost (a `byte`-backed enum cast is free). It does not get you cross-consumer collision detection
— if two consumers share one kernel and pick overlapping role bytes for different meanings,
nothing catches that. That's an accepted cost: no consumer has needed cross-consumer role sharing
yet, and a registry to prevent a problem nobody has hit is exactly the kind of speculative
generality this project avoids elsewhere (see `docs/DECISIONS.md` §1.4 on not adding a
reification depth cap "the store records, it doesn't judge").

## Why not the generic-overload option

The findings doc also considered `AddIncidence<TRole>(source, member, TRole role, int ordinal)
where TRole : unmanaged, Enum`, converting to `byte` inside the kernel. Rejected for M8: it adds a
generic method to the kernel's hot path for an ergonomic win the plain-cast pattern above already
gets almost for free, and it would be the first generic method on `HypergraphKernel` — a real
precedent to set for a one-line convenience. If a third consumer hits friction the plain-cast
pattern doesn't solve, revisit; nothing here is load-bearing enough to lock harder than "this is
the documented pattern."

## If you're the next consumer

Don't define `const byte` role fields with no enum backing them (both `ChatMemory` and
NexusVerifier's original adapter did this before this doc existed). Use a `byte`-backed `enum`
per the pattern above — same runtime shape, better compile-time safety, and it's now the
documented convention other Topos consumers follow.
