using System;
using System.Runtime.CompilerServices;

namespace ProtoZeroSharp;

internal static class VarInt
{
    internal const int MaxBytesCount = 10;
    
    /// <summary>
    /// Writes a variable-length integer to the given output span.
    /// </summary>
    /// <param name="output">Buffer to write varint to</param>
    /// <param name="value">Value to write</param>
    /// <returns>Number of bytes written.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteVarint(Span<byte> output, ulong value)
    {
#if DEBUG
        if (output.Length < MaxBytesCount)
            throw new ArgumentException($"Output buffer must be at least {MaxBytesCount} bytes long to fit any varint");
#endif
        int index = 0;

        while (value >= 0x80)
        {
            output[index++] = (byte)(value | 0x80);
            value >>= 7;
        }
        output[index++] = (byte)value;

        return index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteVarint(Span<byte> output, int value)
    {
        return WriteVarint(output, (ulong)value);
    }
}
