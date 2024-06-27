using System;
using System.Diagnostics;
using System.Text;

namespace ProtoZeroSharp;

[DebuggerTypeProxy(typeof(Utf8StringDebugView))]
public readonly unsafe struct Utf8String : IEquatable<Utf8String>
{
    public readonly int BytesLength;
    private readonly byte* Data;

    public Utf8String(int bytesLength, byte* data)
    {
        BytesLength = bytesLength;
        Data = data;
    }

    public Span<byte> Span => new Span<byte>(Data, BytesLength);

    public override string ToString() => BytesLength == 0 ? "" : Encoding.UTF8.GetString(Data, BytesLength);

    public bool Equals(Utf8String other)
    {
        return BytesLength == other.BytesLength && Span.SequenceEqual(other.Span);
    }

    public override bool Equals(object obj) => obj is Utf8String other && Equals(other);

    public override int GetHashCode()
    {
        return HashCode.Combine(BytesLength, BytesLength > 0 ? Data[0] : 0,
            BytesLength > 1 ? Data[1] : 0,
            BytesLength > 2 ? Data[2] : 0,
            BytesLength > 3 ? Data[3] : 0,
            BytesLength > 4 ? Data[4] : 0,
            BytesLength > 5 ? Data[5] : 0);
    }

    public static bool operator ==(Utf8String left, Utf8String right) => left.Equals(right);

    public static bool operator !=(Utf8String left, Utf8String right) => !left.Equals(right);
}