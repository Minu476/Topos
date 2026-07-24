namespace Topos.Hypergraph.Persistence;

/// <summary>
/// Non-generic handle for a heterogeneous list of typed property columns — lets
/// <see cref="HypergraphSnapshot"/> take one flat <c>IReadOnlyList</c> mixing columns of
/// different <c>T</c>s (a <c>PropertyKey&lt;int&gt;</c> column next to a
/// <c>PropertyKey&lt;string&gt;</c> one) without the caller juggling separate typed lists.
/// </summary>
public interface IPersistedPropertyColumn
{
    string Name { get; }
    void WriteTo(HypergraphKernel kernel, BinaryWriter writer);
    void ReadFrom(HypergraphKernel kernel, BinaryReader reader);
}

internal sealed class PersistedPropertyColumn<T>(
    PropertyKey<T> key, Action<BinaryWriter, T> write, Func<BinaryReader, T> read) : IPersistedPropertyColumn
{
    public string Name => key.Name;

    public void WriteTo(HypergraphKernel kernel, BinaryWriter writer)
    {
        var entries = kernel.EnumerateProperty(key);
        writer.Write(entries.Length);
        foreach (var (handle, value) in entries)
        {
            writer.Write(handle.Index);
            writer.Write(handle.Generation);
            write(writer, value);
        }
    }

    public void ReadFrom(HypergraphKernel kernel, BinaryReader reader)
    {
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var handle = new Handle(reader.ReadUInt32(), reader.ReadUInt32());
            var value = read(reader);
            kernel.SetProperty(key, handle, value);
        }
    }
}

/// <summary>
/// Codecs for common property types, plus <see cref="Custom{T}"/> as the escape hatch for
/// anything else. Deliberately not fully-generic-automatic (spec §6 M4's scope note in
/// <see cref="HypergraphSnapshot"/>): the caller states up front which properties to persist and
/// how, rather than the snapshot machinery trying to introspect arbitrary types.
/// </summary>
public static class PersistedProperty
{
    public static IPersistedPropertyColumn Int32(PropertyKey<int> key) =>
        new PersistedPropertyColumn<int>(key, static (w, v) => w.Write(v), static r => r.ReadInt32());

    public static IPersistedPropertyColumn Int64(PropertyKey<long> key) =>
        new PersistedPropertyColumn<long>(key, static (w, v) => w.Write(v), static r => r.ReadInt64());

    public static IPersistedPropertyColumn Double(PropertyKey<double> key) =>
        new PersistedPropertyColumn<double>(key, static (w, v) => w.Write(v), static r => r.ReadDouble());

    public static IPersistedPropertyColumn Single(PropertyKey<float> key) =>
        new PersistedPropertyColumn<float>(key, static (w, v) => w.Write(v), static r => r.ReadSingle());

    public static IPersistedPropertyColumn Boolean(PropertyKey<bool> key) =>
        new PersistedPropertyColumn<bool>(key, static (w, v) => w.Write(v), static r => r.ReadBoolean());

    public static IPersistedPropertyColumn String(PropertyKey<string> key) =>
        new PersistedPropertyColumn<string>(key, static (w, v) => w.Write(v), static r => r.ReadString());

    public static IPersistedPropertyColumn Byte(PropertyKey<byte> key) =>
        new PersistedPropertyColumn<byte>(key, static (w, v) => w.Write(v), static r => r.ReadByte());

    public static IPersistedPropertyColumn DateOnly(PropertyKey<DateOnly> key) =>
        new PersistedPropertyColumn<DateOnly>(key, static (w, v) => w.Write(v.DayNumber), static r => System.DateOnly.FromDayNumber(r.ReadInt32()));

    /// <summary>Escape hatch: any <c>T</c> not covered above, with a caller-supplied encode/decode pair.</summary>
    public static IPersistedPropertyColumn Custom<T>(
        PropertyKey<T> key, Action<BinaryWriter, T> write, Func<BinaryReader, T> read) =>
        new PersistedPropertyColumn<T>(key, write, read);
}
