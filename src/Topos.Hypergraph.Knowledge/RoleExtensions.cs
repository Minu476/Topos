using System.Runtime.CompilerServices;

namespace Topos.Hypergraph.Knowledge;

/// <summary>
/// Turns `docs/ROLE_CONVENTIONS.md`'s documented byte-backed-enum pattern (M8, finding #3) into
/// real code, per M9's scope (`docs/DECISIONS.md`). No kernel change —
/// <see cref="HypergraphKernel.AddIncidence"/> still only ever takes a raw <c>byte</c>; this is a
/// thin generic wrapper living in this layer-1 package, not a new generic method on the kernel
/// itself. M8 explicitly considered and rejected putting <c>AddIncidence&lt;TRole&gt;</c> on
/// <see cref="HypergraphKernel"/> directly — see `docs/ROLE_CONVENTIONS.md` "Why not the
/// generic-overload option." Living here instead costs nothing and closes the loop that doc left
/// open ("if a third consumer hits friction the plain-cast pattern doesn't solve, revisit").
/// </summary>
public static class RoleExtensions
{
    /// <summary>Casts <paramref name="role"/> to <c>byte</c> before delegating to <see cref="HypergraphKernel.AddIncidence"/> — same runtime shape as a manual cast, just named.</summary>
    public static Incidence AddIncidence<TRole>(this HypergraphKernel kernel, Handle source, Handle member, TRole role, int ordinal)
        where TRole : unmanaged, Enum =>
        kernel.AddIncidence(source, member, RoleToByte(role), ordinal);

    /// <summary><see cref="DirectedTraversal.DirectedBfs"/>, typed-role overload — see `docs/ROLE_CONVENTIONS.md`.</summary>
    public static IReadOnlyList<Handle> DirectedBfs<TRole>(this IHypergraphQuery graph, Handle start, TRole fromRole, TRole toRole)
        where TRole : unmanaged, Enum =>
        graph.DirectedBfs(start, RoleToByte(fromRole), RoleToByte(toRole));

    /// <summary><see cref="DirectedTraversal.DirectedShortestPath"/>, typed-role overload — see `docs/ROLE_CONVENTIONS.md`.</summary>
    public static IReadOnlyList<Handle> DirectedShortestPath<TRole>(this IHypergraphQuery graph, Handle from, Handle to, TRole fromRole, TRole toRole)
        where TRole : unmanaged, Enum =>
        graph.DirectedShortestPath(from, to, RoleToByte(fromRole), RoleToByte(toRole));

    /// <summary><see cref="DirectedTraversal.RoleFilteredMembers"/>, typed-role overload — see `docs/ROLE_CONVENTIONS.md`.</summary>
    public static IReadOnlyList<Handle> RoleFilteredMembers<TRole>(this IHypergraphQuery graph, Handle vertex, TRole role)
        where TRole : unmanaged, Enum =>
        graph.RoleFilteredMembers(vertex, RoleToByte(role));

    /// <summary><see cref="DirectedTraversal.DirectedScc"/>, typed-role overload — see `docs/ROLE_CONVENTIONS.md`.</summary>
    public static IReadOnlyList<IReadOnlyList<Handle>> DirectedScc<TRole>(this IHypergraphQuery graph, TRole fromRole, TRole toRole)
        where TRole : unmanaged, Enum =>
        graph.DirectedScc(RoleToByte(fromRole), RoleToByte(toRole));

    /// <summary>
    /// Reinterprets a byte-backed enum's bit pattern as a <c>byte</c> with no boxing — the "free"
    /// cast `docs/ROLE_CONVENTIONS.md` describes. Throws for any <typeparamref name="TRole"/> not
    /// actually byte-backed (e.g. <c>enum Foo : int</c>) rather than silently reading a truncated
    /// value: the convention this package codifies only ever promised a byte-backed enum, and a
    /// caller passing a wider one is a real mistake worth surfacing, not a case to paper over.
    /// </summary>
    private static byte RoleToByte<TRole>(TRole role) where TRole : unmanaged, Enum
    {
        if (Unsafe.SizeOf<TRole>() != sizeof(byte))
        {
            throw new ArgumentException(
                $"{typeof(TRole)} must be a byte-backed enum (docs/ROLE_CONVENTIONS.md) — " +
                $"found a {Unsafe.SizeOf<TRole>()}-byte underlying type.", nameof(role));
        }
        return Unsafe.As<TRole, byte>(ref role);
    }
}
