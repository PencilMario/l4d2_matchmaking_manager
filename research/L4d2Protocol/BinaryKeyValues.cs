using System.Buffers.Binary;
using System.Text;

internal sealed record BinaryKvEntry(byte Type, string Name, object Value);

internal static class BinaryKeyValues
{
    internal const byte TypeObject = 0;
    internal const byte TypeString = 1;
    internal const byte TypeInt32 = 2;
    internal const byte TypeUInt64 = 7;

    private const byte TypeFloat = 3;
    private const byte TypePointer = 4;
    private const byte TypeColor = 6;
    private const byte TypeCompiledIntByte = 8;
    private const byte TypeCompiledIntZero = 9;
    private const byte TypeCompiledIntOne = 10;
    private const byte TypeEnd = 11;

    internal static BinaryKvEntry Object(string name, params BinaryKvEntry[] children) =>
        new(TypeObject, name, children);

    internal static BinaryKvEntry String(string name, string value) =>
        new(TypeString, name, value);

    internal static BinaryKvEntry Int32(string name, int value) =>
        new(TypeInt32, name, value);

    internal static BinaryKvEntry UInt64(string name, ulong value) =>
        new(TypeUInt64, name, value);

    internal static byte[] Encode(params BinaryKvEntry[] entries)
    {
        var output = new List<byte>(512);
        WriteList(output, entries, littleEndian: false);
        return output.ToArray();
    }

    internal static byte[] EncodeLittleEndian(params BinaryKvEntry[] entries)
    {
        var output = new List<byte>(512);
        WriteList(output, entries, littleEndian: true);
        return output.ToArray();
    }

    internal static bool TryIndex(
        ReadOnlySpan<byte> data,
        int offset,
        out Dictionary<string, object> index,
        out int bytesConsumed)
    {
        index = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        bytesConsumed = 0;
        if (offset < 0 || offset > data.Length)
            return false;

        var current = offset;
        if (!TryReadList(data, ref current, string.Empty, index))
        {
            index.Clear();
            return false;
        }

        bytesConsumed = current - offset;
        return true;
    }

    private static bool TryReadList(
        ReadOnlySpan<byte> data,
        ref int offset,
        string parentPath,
        Dictionary<string, object> index)
    {
        while (offset < data.Length)
        {
            var type = data[offset++];
            if (type == TypeEnd)
                return true;
            if (!TryReadCString(data, ref offset, out var name))
                return false;

            var path = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}/{name}";
            switch (type)
            {
                case TypeObject:
                    index[path] = true;
                    if (!TryReadList(data, ref offset, path, index))
                        return false;
                    break;
                case TypeString:
                    if (!TryReadCString(data, ref offset, out var value))
                        return false;
                    index[path] = value;
                    break;
                case TypeInt32:
                case TypeFloat:
                case TypePointer:
                case TypeColor:
                    if (offset + 4 > data.Length)
                        return false;
                    index[path] = BinaryPrimitives.ReadInt32BigEndian(data[offset..]);
                    offset += 4;
                    break;
                case TypeUInt64:
                    if (offset + 8 > data.Length)
                        return false;
                    index[path] = BinaryPrimitives.ReadUInt64BigEndian(data[offset..]);
                    offset += 8;
                    break;
                case TypeCompiledIntByte:
                    if (offset >= data.Length)
                        return false;
                    index[path] = (int)data[offset++];
                    break;
                case TypeCompiledIntZero:
                    index[path] = 0;
                    break;
                case TypeCompiledIntOne:
                    index[path] = 1;
                    break;
                default:
                    return false;
            }
        }

        return false;
    }

    private static bool TryReadCString(ReadOnlySpan<byte> data, ref int offset, out string value)
    {
        value = string.Empty;
        if (offset >= data.Length)
            return false;

        var terminator = data[offset..].IndexOf((byte)0);
        if (terminator < 0)
            return false;

        value = Encoding.UTF8.GetString(data.Slice(offset, terminator));
        offset += terminator + 1;
        return true;
    }

    private static void WriteList(
        List<byte> output,
        IReadOnlyList<BinaryKvEntry> entries,
        bool littleEndian)
    {
        foreach (var entry in entries)
        {
            output.Add(entry.Type);
            WriteCString(output, entry.Name);
            switch (entry.Type)
            {
                case TypeObject:
                    WriteList(output, (IReadOnlyList<BinaryKvEntry>)entry.Value, littleEndian);
                    break;
                case TypeString:
                    WriteCString(output, (string)entry.Value);
                    break;
                case TypeInt32:
                    WriteInt32(output, (int)entry.Value, littleEndian);
                    break;
                case TypeUInt64:
                    WriteUInt64(output, (ulong)entry.Value, littleEndian);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported outgoing KeyValues type {entry.Type}.");
            }
        }

        output.Add(TypeEnd);
    }

    private static void WriteCString(List<byte> output, string value)
    {
        output.AddRange(Encoding.UTF8.GetBytes(value));
        output.Add(0);
    }

    private static void WriteInt32(List<byte> output, int value, bool littleEndian)
    {
        if (littleEndian)
        {
            output.Add((byte)value);
            output.Add((byte)(value >> 8));
            output.Add((byte)(value >> 16));
            output.Add((byte)(value >> 24));
            return;
        }

        output.Add((byte)(value >> 24));
        output.Add((byte)(value >> 16));
        output.Add((byte)(value >> 8));
        output.Add((byte)value);
    }

    private static void WriteUInt64(List<byte> output, ulong value, bool littleEndian)
    {
        if (littleEndian)
        {
            for (var shift = 0; shift <= 56; shift += 8)
                output.Add((byte)(value >> shift));
            return;
        }

        for (var shift = 56; shift >= 0; shift -= 8)
            output.Add((byte)(value >> shift));
    }
}
