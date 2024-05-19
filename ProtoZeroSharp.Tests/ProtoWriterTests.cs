using System.Text;

namespace ProtoZeroSharp.Tests;

public class ProtoWriterTests
{
    private byte[] output = new byte[256 * 1024 * 1024];

    [Test]
    public unsafe void Encode_Zero_Decode_Canonical()
    {
        ProtoWriter writer = new ProtoWriter();

        for (int j = 0; j < 1000; ++j)
        {
            writer.AddVarInt(TestMessage.AFieldNumber, ulong.MaxValue);
            writer.AddVarInt(TestMessage.BFieldNumber, long.MinValue);
            writer.AddBytes(TestMessage.DFieldNumber, "Hello, World!"u8);
            writer.AddString(TestMessage.EFieldNumber, "Msg 1");
            writer.AddBytes(TestMessage.EFieldNumber, "Msg 2"u8);
            writer.AddBytes(TestMessage.EFieldNumber, "Msg 3"u8);
            writer.AddBytes(TestMessage.EFieldNumber, "Msg 4"u8);

            for (int i = 0; i < 3; ++i)
            {
                writer.StartSub(TestMessage.FFieldNumber);

                writer.AddVarInt(SubMessage.IdFieldNumber, i);
                writer.AddBytes(SubMessage.NameFieldNumber, Encoding.UTF8.GetBytes($"Name {i}"));

                writer.CloseSub();
            }
        }

        var buffer = new byte[writer.GetTotalLength()];
        writer.CopyTo(buffer);

        writer.Free();

        var msg = TestMessage.Parser.ParseFrom(buffer);
    }
}