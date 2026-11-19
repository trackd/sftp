using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace sftp.TestServer;

/// <summary>Reads SSH wire-format values (RFC 4251 section 5).</summary>
internal sealed class SshReader(byte[] data, int offset = 0) {
    private int _position = offset;

    public bool HasMore => _position < data.Length;

    public byte ReadByte() => data[_position++];

    public bool ReadBoolean() => ReadByte() != 0;

    public uint ReadUInt32() {
        uint value = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(_position));
        _position += 4;

        return value;
    }

    public ulong ReadUInt64() {
        ulong value = BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(_position));
        _position += 8;

        return value;
    }

    public byte[] ReadBytes(int count) {
        byte[] value = data.AsSpan(_position, count).ToArray();
        _position += count;

        return value;
    }

    public byte[] ReadBinary() => ReadBytes(checked((int)ReadUInt32()));

    public string ReadString() => Encoding.UTF8.GetString(ReadBinary());

    /// <summary>Reads an mpint as an unsigned big-endian value without the sign byte.</summary>
    public byte[] ReadMPInt() {
        byte[] value = ReadBinary();
        int skip = 0;
        while (skip < value.Length - 1 && value[skip] == 0) {
            skip++;
        }

        return value[skip..];
    }
}

/// <summary>Writes SSH wire-format values.</summary>
internal sealed class SshWriter {
    private readonly ArrayBufferWriter<byte> _buffer = new();

    public SshWriter Byte(byte value) {
        _buffer.Write([value]);

        return this;
    }

    public SshWriter Boolean(bool value) => Byte(value ? (byte)1 : (byte)0);

    public SshWriter UInt32(uint value) {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        _buffer.Write(buffer);

        return this;
    }

    public SshWriter UInt64(ulong value) {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
        _buffer.Write(buffer);

        return this;
    }

    public SshWriter Raw(ReadOnlySpan<byte> value) {
        _buffer.Write(value);

        return this;
    }

    public SshWriter Binary(ReadOnlySpan<byte> value) {
        UInt32((uint)value.Length);

        return Raw(value);
    }

    public SshWriter String(string value) => Binary(Encoding.UTF8.GetBytes(value));

    /// <summary>Writes an unsigned big-endian integer as an mpint.</summary>
    public SshWriter MPInt(ReadOnlySpan<byte> value) {
        int skip = 0;
        while (skip < value.Length && value[skip] == 0) {
            skip++;
        }

        ReadOnlySpan<byte> trimmed = value[skip..];
        if (trimmed.Length > 0 && (trimmed[0] & 0x80) != 0) {
            UInt32((uint)trimmed.Length + 1);
            Byte(0);

            return Raw(trimmed);
        }

        return Binary(trimmed);
    }

    public byte[] ToArray() => _buffer.WrittenSpan.ToArray();
}
