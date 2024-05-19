using System;
using System.IO;

namespace ProtoZeroSharp;

/// <summary>
/// Extremely unsafe but pretty fast chunked array implementation.
/// Entirely unmanaged, allocates memory manually using Marshal.AllocHGlobal,
/// therefore the user HAS TO call Free() to free the memory, but thanks to this,
/// the implementation is very fast and doesn't generate ANY garbage.
///
/// Additionally, this implementation maintains a current position in the array,
/// which can be moved forward, but you should never Move Forward more than you previously
/// reserved using TakeContiguousSpan.
/// </summary>
internal unsafe partial struct ChunkedArray
{
    private const int DefaultChunkSize = 16384;

    private Chunk* first;
    private Chunk* last;

    /// <summary>
    /// Creates a new ChunkedArray with a default chunk size of 4096 bytes.
    /// </summary>
    public ChunkedArray()
    {
        first = Chunk.AllocChunk(DefaultChunkSize);
        last = first;
    }

    /// <summary>
    /// Frees all the memory allocated by this ChunkedArray.
    /// </summary>
    public void Free()
    {
        Chunk.FreeChunksChain(first);
        first = last = null;
    }

    /// <summary>
    /// Returns the current position in the ChunkedArray.
    /// </summary>
    public ChunkOffset Position => new ChunkOffset(last, last->Used);

    /// <summary>
    /// Returns a contiguous span of bytes with the given length starting at the current position.
    /// Doesn't move the current position.
    /// If the current chunk doesn't have enough space, allocates a new chunk.
    /// </summary>
    /// <exception cref="ObjectDisposedException"></exception>
    public Span<byte> ReserveContiguousSpan(int length)
    {
        AssertNotDisposed();

        if (last->FreeBytes < length)
        {
            last->Next = Chunk.AllocChunk(Math.Max(DefaultChunkSize, length));
            last = last->Next;
        }

        var span = last->GetSpan(last->Used, length);
        return span;
    }

    /// <summary>
    /// Moves the current position forward by the given length.
    /// You should only call this method after you reserved a contiguous span using ReserveContiguousSpan.
    /// And you should never exceed the length you reserved, as it may result in InvalidOperationException exception.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public void MoveForward(int length)
    {
        AssertNotDisposed();

        if (last->Used + length > last->Length)
            throw new InvalidOperationException("Moving forward would exceed the current chunk's capacity.");
        last->Used += length;
    }

    /// <summary>
    /// Calculates the total length of all the chunks in this ChunkedArray.
    /// </summary>
    public int GetTotalLength()
    {
        AssertNotDisposed();

        int totalLength = 0;
        var chunk = first;
        while (chunk != null)
        {
            totalLength += chunk->Used;
            chunk = chunk->Next;
        }

        return totalLength;
    }

    /// <summary>
    /// Copies the entire content of this ChunkedArray to the given destination span.
    /// </summary>
    /// <param name="destination"></param>
    public int CopyTo(Span<byte> destination)
    {
        var chunk = first;
        int copiedBytes = 0;
        while (chunk != null)
        {
            chunk->GetSpan().CopyTo(destination);
            destination = destination.Slice(chunk->Used);
            copiedBytes += chunk->Used;
            chunk = chunk->Next;
        }

        return copiedBytes;
    }

    /// <summary>
    /// Writes the entire content of this ChunkedArray to the given stream.
    /// </summary>
    /// <param name="stream"></param>
    public void WriteTo(Stream stream)
    {
        var chunk = first;
#if !NET5_0_OR_GREATER
        byte[] tempArray = new byte[chunk == null ? 0 : chunk->Length];
#endif
        while (chunk != null)
        {
#if NET5_0_OR_GREATER
            stream.Write(chunk->GetSpan());
#else
            if (chunk->Length > tempArray.Length)
                tempArray = new byte[chunk->Length];
            chunk->GetSpan().CopyTo(tempArray);
            stream.Write(tempArray, 0, chunk->Used);
#endif
            chunk = chunk->Next;
        }
    }

    private void AssertNotDisposed()
    {
        if (last == null)
            throw new ObjectDisposedException("This ChunkedArray has been disposed.");
    }
}
