using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ProtoZeroSharp;

/// <summary>
/// Provides methods to write protocol buffer messages.
/// </summary>
public ref struct ProtoWriter
{
    private readonly ref ArenaAllocator memory;
    private StackArray<ArenaAllocator.ChunkOffset> submessagesStack;
    private StackArray<int> lengthsStack;
    private int currentMessageLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtoWriter"/> struct.
    /// </summary>
    /// <param name="memory">Reference to an <see cref="ArenaAllocator"/> for memory allocation.</param>
    public ProtoWriter(ref ArenaAllocator memory)
    {
        this.memory = ref memory;
        currentMessageLength = 0;
        submessagesStack = new StackArray<ArenaAllocator.ChunkOffset>();
        lengthsStack = new StackArray<int>();
    }

    /// <summary>
    /// Gets the total length of the written message.
    /// </summary>
    /// <returns>The total length of the message in bytes.</returns>
    public int GetTotalLength() => memory.GetTotalLength();

    /// <summary>
    /// Copies the written message to the specified span.
    /// </summary>
    /// <param name="destination">The span to copy the message to.</param>
    /// <returns>The number of bytes copied.</returns>
    public int CopyTo(Span<byte> destination) => memory.CopyTo(destination);

    /// <summary>
    /// Writes the message to the specified stream.
    /// </summary>
    /// <param name="stream">The stream to write the message to.</param>
    public void WriteTo(Stream stream) => memory.WriteTo(stream);

    /// <summary>
    /// Adds a float field to the message.
    /// </summary>
    /// <param name="messageId">The message ID of the field.</param>
    /// <param name="value">The float value to add.</param>
    public void AddFloat(int messageId, float value)
    {
        var span = memory.ReserveContiguousSpan(ProtobufFormat.Fixed32FieldLenUpperBound);
        int written = ProtobufFormat.WriteFloatField(span, messageId, value);
        memory.MoveForward(written);
        currentMessageLength += written;
    }

    /// <summary>
    /// Adds a double field to the message.
    /// </summary>
    /// <param name="messageId">The message ID of the field.</param>
    /// <param name="value">The double value to add.</param>
    public void AddDouble(int messageId, double value)
    {
        var span = memory.ReserveContiguousSpan(ProtobufFormat.Fixed64FieldLenUpperBound);
        int written = ProtobufFormat.WriteDoubleField(span, messageId, value);
        memory.MoveForward(written);
        currentMessageLength += written;
    }

    /// <summary>
    /// Adds a varint field to the message.
    /// </summary>
    /// <param name="messageId">The message ID of the field.</param>
    /// <param name="value">The long value to add.</param>
    public void AddVarInt(int messageId, long value) => AddVarInt(messageId, (ulong)value);

    /// <summary>
    /// Adds a varint field to the message.
    /// </summary>
    /// <param name="messageId">The message ID of the field.</param>
    /// <param name="value">The ulong value to add.</param>
    public void AddVarInt(int messageId, ulong value)
    {
        var span = memory.ReserveContiguousSpan(ProtobufFormat.VarIntFieldLenUpperBound);
        int written = ProtobufFormat.WriteVarIntField(span, messageId, value);
        memory.MoveForward(written);
        currentMessageLength += written;
    }

    /// <summary>
    /// Adds a bytes field to the message.
    /// </summary>
    /// <param name="messageId">The message ID of the field.</param>
    /// <param name="payload">The byte span to add.</param>
    public void AddBytes(int messageId, ReadOnlySpan<byte> payload)
    {
        // this could be optimized to write in chunks, but needs a better API
        var span = memory.ReserveContiguousSpan(ProtobufFormat.BytesFieldLenUpperBound(payload.Length));
        int written = ProtobufFormat.WriteBytes(span, messageId, payload);
        memory.MoveForward(written);
        currentMessageLength += written;
    }

    /// <summary>
    /// Adds a string field to the message.
    /// </summary>
    /// <param name="messageId">The message ID of the field.</param>
    /// <param name="payload">The string value to add.</param>
    public void AddString(int messageId, string payload)
    {
        var payloadLength = Encoding.UTF8.GetByteCount(payload);
        // this could be optimized to write in chunks, but needs a better API
        var span = memory.ReserveContiguousSpan(ProtobufFormat.BytesFieldLenUpperBound(payloadLength));
        int written = ProtobufFormat.WriteString(span, messageId, payloadLength, payload);
        memory.MoveForward(written);
        currentMessageLength += written;
    }

    /// <summary>
    /// Starts a submessage with the specified message ID.
    /// </summary>
    /// <param name="messageId">The message ID of the submessage.</param>
    public void StartSub(int messageId)
    {
        var headerSpan = memory.ReserveContiguousSpan(ProtobufFormat.SubMessageHeaderLenUpperBound);
        int written = ProtobufFormat.WriteLengthFieldHeader(headerSpan, messageId);
        currentMessageLength += written;
        memory.MoveForward(written);
        submessagesStack.Add(memory.Position);
        lengthsStack.Add(currentMessageLength);
        memory.MoveForward(VarInt.MaxBytesCount); // reserve space for the length
        currentMessageLength = 0;
    }

    /// <summary>
    /// Closes the most recently started submessage.
    /// </summary>
    /// <param name="optimizeSizeOverPerformance">Specifies whether to optimize size over performance. Default is true.</param>
    public unsafe void CloseSub(bool optimizeSizeOverPerformance = true)
    {
        var lastSubMessageStart = submessagesStack.PeekAndPop();

        var lengthSpan = lastSubMessageStart.Chunk->GetSpan(lastSubMessageStart.Offset);
        int written;
        if (optimizeSizeOverPerformance)
        {
            written = ProtobufFormat.WriteLengthFieldLength(lengthSpan, currentMessageLength);
            if (written < VarInt.MaxBytesCount)
            {
                lastSubMessageStart.Chunk->Erase(lastSubMessageStart.Offset + written, VarInt.MaxBytesCount - written);
            }
        }
        else
        {
            written = ProtobufFormat.WriteLengthFieldLength(lengthSpan, currentMessageLength, VarInt.MaxBytesCount);
            Debug.Assert(written == VarInt.MaxBytesCount);
        }

        currentMessageLength = lengthsStack.PeekAndPop() + written + currentMessageLength;
    }
}