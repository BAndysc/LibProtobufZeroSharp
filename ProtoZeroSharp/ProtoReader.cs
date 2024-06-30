using System;
using System.Runtime.InteropServices;

namespace ProtoZeroSharp;

/// <summary>
/// Represents a reader that parses serialized data in Protocol Buffers format.
/// </summary>
public ref struct ProtoReader
{
    private readonly ReadOnlySpan<byte> memory;
    private int offset;
    private int currentField;
    private ProtoWireType currentWireType;

    /// <summary>Gets the number of bytes remaining from the current offset to the end of the memory span.</summary>
    public int Remaining => memory.Length - offset;

    /// <summary>Gets the tag of the current field being processed.</summary>
    public int Tag => currentField;

    /// <summary>Gets the current wire type of the field being processed.</summary>
    public ProtoWireType WireType => currentWireType;

    /// <summary>
    /// Initializes a new instance of the ProtoReader class with the specified memory span.
    /// </summary>
    /// <param name="memory">The span of bytes containing the serialized data.</param>
    public ProtoReader(ReadOnlySpan<byte> memory)
    {
        this.memory = memory;
        offset = 0;
        currentField = 0;
        currentWireType = ProtoWireType.VarInt;
    }

    private ReadOnlySpan<byte> GetSpan(int maxLength = -1)
        => memory.Slice(offset, maxLength == -1 ? Remaining : Math.Min(maxLength, Remaining));

    private void MoveForward(int length)
        => offset += length;

    /// <summary>
    /// Advances the reader to the next field in the serialized data.
    /// </summary>
    /// <returns>true if there are more fields to read; false if the end of the memory span is reached.</returns>
    public bool Next()
    {
        var tagSpan = GetSpan(VarInt.MaxBytesCount);
        if (tagSpan.Length == 0)
            return false;

        MoveForward(ProtobufFormat.ReadFieldHeader(tagSpan, out currentField, out currentWireType));
        return true;
    }

    private void EnsureWireType(ProtoWireType wireType)
    {
        if (currentWireType != wireType)
            throw new InvalidOperationException($"Current wire type is not {wireType}");
    }

    /// <summary>
    /// Reads a variable-length integer from the current position.
    /// This method should only be called if you know the current field is a varint.
    /// In Debug mode, this method will throw an exception if the current wire type is not ProtoWireType.VarInt,
    /// but in Release mode it will not check the wire type.
    /// </summary>
    /// <returns>The decoded unsigned long integer.</returns>
    public ulong ReadVarInt()
    {
#if DEBUG
        EnsureWireType(ProtoWireType.VarInt);
#endif
        if ((memory[offset] & 0x80) == 0) // fast path
        {
            offset++;
            return memory[offset - 1];
        }
        MoveForward(VarInt.ReadVarint(GetSpan(), out var value));
        return value;
    }

    /// <summary>
    /// Reads a 32-bit floating point number from the current position.
    /// This method should only be called if you know the current field is a float.
    /// In Debug mode, this method will throw an exception if the current wire type is not ProtoWireType.Fixed32,
    /// but in Release mode it will not check the wire type.
    /// </summary>
    /// <returns>The decoded float.</returns>
    public float ReadFloat()
    {
#if DEBUG
        EnsureWireType(ProtoWireType.Fixed32);
#endif
        var span = GetSpan(4);
        MoveForward(4);
        return MemoryMarshal.Read<float>(span);
    }

    /// <summary>
    /// Reads a 64-bit floating point number from the current position.
    /// This method should only be called if you know the current field is a double.
    /// In Debug mode, this method will throw an exception if the current wire type is not ProtoWireType.Fixed64,
    /// but in Release mode it will not check the wire type.
    /// </summary>
    /// <returns>The decoded double.</returns>
    public double ReadDouble()
    {
#if DEBUG
        EnsureWireType(ProtoWireType.Fixed64);
#endif
        var span = GetSpan(8);
        MoveForward(8);
        return MemoryMarshal.Read<double>(span);
    }

    /// <summary>
    /// Reads a boolean value from the current position.
    /// This method should only be called if you know the current field is a varint.
    /// In Debug mode, this method will throw an exception if the current wire type is not ProtoWireType.VarInt,
    /// but in Release mode it will not check the wire type.
    /// </summary>
    /// <returns>true if the read integer is not zero; otherwise, false.</returns>
    public bool ReadBool()
    {
        return ReadVarInt() != 0;
    }

    /// <summary>
    /// Reads a sequence of bytes from the current position, based on the length prefix.
    /// This method should only be called if you know the current field is a Length Message Type.
    /// In Debug mode, this method will throw an exception if the current wire type is not ProtoWireType.Length,
    /// but in Release mode it will not check the wire type.
    /// </summary>
    /// <returns>A span of bytes representing the read data.</returns>
    public ReadOnlySpan<byte> ReadBytes()
    {
#if DEBUG
        EnsureWireType(ProtoWireType.Length);
#endif
        MoveForward(VarInt.ReadVarint(GetSpan(), out var len));
        var span = GetSpan((int)len);
        MoveForward((int)len);
        return span;
    }

    /// <summary>
    /// Reads a nested message from the current position and initializes a new ProtoReader for it.
    /// This method should only be called if you know the current field is a Length Message Type and it encodes a nested message.
    /// In Debug mode, this method will throw an exception if the current wire type is not ProtoWireType.Length,
    /// but in Release mode it will not check the wire type.
    /// </summary>
    /// <returns>A new ProtoReader instance for the nested message.</returns>
    public ProtoReader ReadMessage()
    {
#if DEBUG
        EnsureWireType(ProtoWireType.Length);
#endif
        MoveForward(VarInt.ReadVarint(GetSpan(), out var len));
        var reader = new ProtoReader(memory.Slice(offset, (int)len));
        MoveForward((int)len);
        return reader;
    }

    /// <summary>
    /// Skips the current field, moving the read position forward past the field data.
    /// </summary>
    public void Skip()
    {
        switch (currentWireType)
        {
            case ProtoWireType.VarInt:
                MoveForward(VarInt.ReadVarint(GetSpan(), out _));
                break;
            case ProtoWireType.Fixed64:
                MoveForward(8);
                break;
            case ProtoWireType.Length:
                MoveForward(VarInt.ReadVarint(GetSpan(), out var len));
                MoveForward((int)len);
                break;
            case ProtoWireType.StartGroup:
                throw new NotImplementedException();
            case ProtoWireType.EndGroup:
                throw new NotImplementedException();
            case ProtoWireType.Fixed32:
                MoveForward(4);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}