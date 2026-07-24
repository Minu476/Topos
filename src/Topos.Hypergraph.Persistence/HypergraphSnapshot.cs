using System.Text;

namespace Topos.Hypergraph.Persistence;

/// <summary>
/// M4's persistence mechanism (spec §6 M4 — first slice; see the scope note below). Serializes a
/// <see cref="HypergraphKernel"/>'s full topology (vertices, incidences, Handle-allocator state)
/// plus an explicit, caller-specified set of typed property columns, to and from a
/// <see cref="Stream"/> in a simple versioned binary format.
///
/// <b>Columnar for properties</b> (spec's "columnar on-disk, Kuzu's pattern"): each property
/// column is written as its own contiguous (Handle, Value) run, mirroring how
/// <c>SparseSet&lt;T&gt;</c> already stores it in memory (dense parallel arrays) — the on-disk
/// shape follows the in-memory shape almost for free, rather than needing a separate columnar
/// engine built from scratch.
///
/// <b>Invariant 1, preserved across reload.</b> <see cref="Save"/> writes
/// <see cref="HypergraphKernel.NextHandleIndex"/>; <see cref="Load"/> constructs the new kernel
/// with that value as its allocator's starting point (<see cref="HandleAllocator"/>'s
/// constructor), so the first vertex created after reload gets a genuinely fresh Index — never
/// one that collides with a restored vertex. <see cref="HypergraphKernel.RestoreVertex"/> is the
/// one bypass of the normal <c>CreateVertex</c> allocation path, used only here, only because the
/// Handles being restored were already validly allocated once, by the kernel that was saved.
///
/// <b>Scope, stated plainly — what this is and isn't.</b> This proves M4's core exit-criterion
/// claim: a graph's structure can live on disk and be reloaded correctly, with Handle identity
/// intact. It is <i>not</i>:
/// <list type="bullet">
/// <item>A transparent hot/cold hybrid kernel. A live <see cref="HypergraphKernel"/> doesn't
/// automatically spill to disk under memory pressure — this is an explicit save/load step, not a
/// background tiering system.</item>
/// <item>An LSM tree. No compaction, no write-ahead log, no crash safety beyond "the file write
/// completed" (a torn write from a crash mid-<see cref="Save"/> is not handled).</item>
/// <item>Automatically generic over property types. Common types have codecs
/// (<see cref="PersistedProperty"/>); anything else needs
/// <see cref="PersistedProperty.Custom{T}"/>.</item>
/// </list>
/// Building the full tiered/transparent version — the actual "graphs larger than RAM work"
/// experience where you never manually call Save/Load — is real follow-on work, deliberately not
/// attempted in one sitting without dedicated design review. Same discipline as the M0 CSR-tier
/// decision: measure and design before committing to that scope, don't build it speculatively.
/// <see cref="LruCache{TKey,TValue}"/> is the tested hot-tier building block that work would
/// start from.
/// </summary>
public static class HypergraphSnapshot
{
    private const uint Magic = 0x53485054; // ASCII "TPHS" read little-endian
    private const int FormatVersion = 1;

    public static void Save(HypergraphKernel kernel, Stream stream, IReadOnlyList<IPersistedPropertyColumn>? properties = null)
    {
        properties ??= [];
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(Magic);
        writer.Write(FormatVersion);
        writer.Write(kernel.NextHandleIndex);

        var vertices = kernel.VertexHandles();
        writer.Write(vertices.Count);
        foreach (var h in vertices)
        {
            kernel.TryGetVertex(h, out var v);
            writer.Write(h.Index);
            writer.Write(h.Generation);
            writer.Write((byte)v.Roles);
            writer.Write((byte)v.Status);
        }

        var incidences = kernel.AllIncidences().ToList();
        writer.Write(incidences.Count);
        foreach (var i in incidences)
        {
            writer.Write(i.Source.Index);
            writer.Write(i.Source.Generation);
            writer.Write(i.Member.Index);
            writer.Write(i.Member.Generation);
            writer.Write(i.Role);
            writer.Write(i.Ordinal);
        }

        writer.Write(properties.Count);
        foreach (var column in properties)
        {
            writer.Write(column.Name);
            column.WriteTo(kernel, writer);
        }
    }

    public static HypergraphKernel Load(Stream stream, IReadOnlyList<IPersistedPropertyColumn>? properties = null)
    {
        properties ??= [];
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        uint magic = reader.ReadUInt32();
        if (magic != Magic)
            throw new InvalidDataException("Not a Topos hypergraph snapshot (bad magic number).");

        int version = reader.ReadInt32();
        if (version != FormatVersion)
            throw new InvalidDataException($"Unsupported snapshot format version {version} (this build supports {FormatVersion}).");

        uint nextHandleIndex = reader.ReadUInt32();
        var kernel = new HypergraphKernel(nextHandleIndex);

        int vertexCount = reader.ReadInt32();
        for (int i = 0; i < vertexCount; i++)
        {
            var handle = new Handle(reader.ReadUInt32(), reader.ReadUInt32());
            var roles = (VertexRoles)reader.ReadByte();
            var status = (VertexStatus)reader.ReadByte();
            kernel.RestoreVertex(handle, roles, status);
        }

        int incidenceCount = reader.ReadInt32();
        for (int i = 0; i < incidenceCount; i++)
        {
            var source = new Handle(reader.ReadUInt32(), reader.ReadUInt32());
            var member = new Handle(reader.ReadUInt32(), reader.ReadUInt32());
            byte role = reader.ReadByte();
            int ordinal = reader.ReadInt32();
            kernel.AddIncidence(source, member, role, ordinal);
        }

        int columnCount = reader.ReadInt32();
        var byName = properties.ToDictionary(c => c.Name);
        for (int i = 0; i < columnCount; i++)
        {
            string name = reader.ReadString();
            if (!byName.TryGetValue(name, out var column))
                throw new InvalidDataException(
                    $"Snapshot has property column '{name}' but no matching column descriptor was passed to Load — " +
                    "every column written by Save must be named in Load's properties list, or the bytes can't be decoded.");
            column.ReadFrom(kernel, reader);
        }

        return kernel;
    }
}
