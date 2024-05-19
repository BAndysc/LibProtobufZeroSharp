using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Google.Protobuf;
using ProtoZeroSharp;
using ProtoZeroSharp.Tests;

namespace Benchmarks;

[SimpleJob]
[MemoryDiagnoser]
public partial class ProtoZeroVsCanonical
{
    private byte[] output = new byte[64 * 1024 * 1024]; // I have precalculated the message and I know this is enough to store it all

    [LibraryImport("protozero", EntryPoint = "WriteProto")]
    public static unsafe partial int NativeProto(int messagesCount, byte* output);

    [Benchmark]
    public unsafe void ProtoZeroNative()
    {
        try
        {
            fixed (byte* outputPtr = output)
                NativeProto(200000, outputPtr);
        }
        catch (DllNotFoundException e)
        {
            throw new Exception("Build native library with native/build.sh or native/build.cmd depending on platform to enable this test.");
        }
    }

    [Benchmark]
    public void ProtoZeroSharp()
    {
        ProtoWriter writer = new ProtoWriter();

        for (int j = 0; j < 200000; ++j)
        {
            writer.StartSub(RootMessage.MessagesFieldNumber);
            writer.AddVarInt(TestMessage.AFieldNumber, ulong.MaxValue);
            writer.AddVarInt(TestMessage.BFieldNumber, long.MinValue);
            writer.AddBytes(TestMessage.DFieldNumber, "Hello, World!"u8);
            writer.AddBytes(TestMessage.EFieldNumber, "Msg 1"u8);
            writer.AddBytes(TestMessage.EFieldNumber, "Msg 2"u8);
            writer.AddBytes(TestMessage.EFieldNumber, "Msg 3"u8);
            writer.AddBytes(TestMessage.EFieldNumber, "Msg 4"u8);

            for (int i = 0; i < 9; ++i)
            {
                writer.StartSub(TestMessage.FFieldNumber);

                writer.AddVarInt(SubMessage.IdFieldNumber, i);
                writer.AddBytes(SubMessage.NameFieldNumber, "Inner Message"u8);

                writer.CloseSub();
            }

            writer.CloseSub();
        }

        writer.CopyTo(output);

        writer.Free();
    }

    [Benchmark(Baseline = true)]
    public void CanonicalProto()
    {
        RootMessage root = new RootMessage();

        for (int j = 0; j < 200000; ++j)
        {
            TestMessage message = new TestMessage();
            message.A = ulong.MaxValue;
            message.B = long.MinValue;
            message.D = "Hello, World!";
            message.E.Add("Msg 1");
            message.E.Add("Msg 2");
            message.E.Add("Msg 3");
            message.E.Add("Msg 4");

            for (int i = 0; i < 9; ++i)
            {
                SubMessage subMessage = new SubMessage();
                subMessage.Id = i;
                subMessage.Name = "Inner Message";
                message.F.Add(subMessage);
            }
            root.Messages.Add(message);
        }

        root.WriteTo(output.AsSpan(0, root.CalculateSize()));
    }
}