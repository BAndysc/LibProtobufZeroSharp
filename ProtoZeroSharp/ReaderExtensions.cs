using System;

namespace ProtoZeroSharp;

public static class ReaderExtensions
{
    /// <summary>
    /// Reads a string from the current position.
    /// </summary>
    /// <returns>The decoded string.</returns>
    public static string ReadString(this ref ProtoReader reader)
    {
        var bytes = reader.ReadBytes();
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Reads a UTF-8 encoded string from the <see cref="ProtoReader"/> and stores it using the specified allocator.
    /// </summary>
    /// <typeparam name="TAllocator">The type of the allocator, which must implement <see cref="IAllocator"/>.</typeparam>
    /// <param name="reader">The <see cref="ProtoReader"/> to read from.</param>
    /// <param name="memory">The allocator to use for storing the UTF-8 string.</param>
    /// <returns>A <see cref="Utf8String"/> representing the read UTF-8 string.</returns>
    public static unsafe Utf8String ReadUtf8String<TAllocator>(this ref ProtoReader reader, ref TAllocator memory) where TAllocator : unmanaged, IAllocator
    {
        var utf8Bytes = reader.ReadBytes();
        if (utf8Bytes.Length == 0)
        {
            return new Utf8String(0, null);
        }
        else
        {
            var data = memory.Allocate<byte>(utf8Bytes.Length);
            var span = new Span<byte>(data, utf8Bytes.Length);
            utf8Bytes.CopyTo(span);
            return new Utf8String(utf8Bytes.Length, data);
        }
    }

    /// <summary>
    /// Reads a byte array from the <see cref="ProtoReader"/> and stores it using the specified allocator.
    /// </summary>
    /// <typeparam name="TAllocator">The type of the allocator, which must implement <see cref="IAllocator"/>.</typeparam>
    /// <param name="reader">The <see cref="ProtoReader"/> to read from.</param>
    /// <param name="memory">The allocator to use for storing the byte array.</param>
    /// <returns>An <see cref="UnmanagedArray{T}"/> containing the read byte array.</returns>
    public static unsafe UnmanagedArray<byte> ReadBytesArray<TAllocator>(this ref ProtoReader reader, ref TAllocator memory) where TAllocator : unmanaged, IAllocator
    {
        var bytes = reader.ReadBytes();
        var data = memory.Allocate<byte>(bytes.Length);
        var span = new Span<byte>(data, bytes.Length);
        bytes.CopyTo(span);
        return new UnmanagedArray<byte>(bytes.Length, data);
    }
}