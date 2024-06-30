using System;
using System.Diagnostics;
using System.Text;

namespace ProtoZeroSharp;

/// <summary>
/// Low level, unmanaged view for a UTF-8 string.
/// Doesn't own the memory.
/// </summary>
[DebuggerTypeProxy(typeof(Utf8StringDebugView))]
public readonly unsafe struct Utf8String : IEquatable<Utf8String>
{
    public readonly int BytesLength;
    private readonly byte* data;

    public Utf8String(int bytesLength, byte* data)
    {
        BytesLength = bytesLength;
        this.data = data;
    }

    public Span<byte> Span
        => new(data, BytesLength);

    public override string ToString()
        => BytesLength == 0 ? "" : Encoding.UTF8.GetString(data, BytesLength);

    public bool Equals(Utf8String other)
        => BytesLength == other.BytesLength && Span.SequenceEqual(other.Span);

    public override bool Equals(object obj)
        => obj is Utf8String other && Equals(other);

    public static bool operator ==(Utf8String left, Utf8String right)
        => left.Equals(right);

    public static bool operator !=(Utf8String left, Utf8String right)
        => !left.Equals(right);

    public override int GetHashCode()
    {
        return HashCode.Combine(BytesLength, BytesLength > 0 ? data[0] : 0,
            BytesLength > 1 ? data[1] : 0,
            BytesLength > 2 ? data[2] : 0,
            BytesLength > 3 ? data[3] : 0,
            BytesLength > 4 ? data[4] : 0,
            BytesLength > 5 ? data[5] : 0);
    }
}