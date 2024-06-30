using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Google.Protobuf;
using ProtoZeroSharp;
using ProtoZeroSharp.Tests;

namespace Benchmarks;

[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
public partial class ProtoZeroVsCanonical
{
    private byte[] output = new byte[128 * 1024 * 1024]; // I have precalculated the message and I know this is enough to store it all

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
    public unsafe void PerfectSerializer()
    {
#if NET8_0
        ArenaAllocator array = new ArenaAllocator();
        for (int j = 0; j < 200000; ++j)
        {
            var span = array.ReserveContiguousSpan(sizeof(PerfectSerializer.RootMessageStruct));

            ref PerfectSerializer.RootMessageStruct refStruct = ref Unsafe.As<byte, PerfectSerializer.RootMessageStruct>(ref span.GetPinnableReference());

            refStruct.a = ulong.MaxValue;
            refStruct.b = long.MinValue;
            "Hello, World!"u8.CopyTo(refStruct.d);
            "Msg 1"u8.CopyTo(refStruct.e1);
            "Msg 2"u8.CopyTo(refStruct.e2);
            "Msg 3"u8.CopyTo(refStruct.e3);
            "Msg 4"u8.CopyTo(refStruct.e4);

            for (int i = 0; i < 9; ++i)
            {
                refStruct.f[i].id = i;
                "Inner Message"u8.CopyTo(refStruct.f[i].name);
            }
        }

        array.CopyTo(output);

        array.Dispose();
#endif
    }

    [Benchmark]
    [Arguments(true)]
    [Arguments(false)]
    public void ProtoZeroSharp(bool optimizeSizeOverPerformance)
    {
        ArenaAllocator allocator = new();
        ProtoWriter writer = new ProtoWriter(ref allocator);

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

                writer.CloseSub(optimizeSizeOverPerformance);
            }

            writer.CloseSub(optimizeSizeOverPerformance);
        }

        writer.CopyTo(output);

        allocator.Dispose();
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

    [Benchmark]
    public void ProtobufNet()
    {
        var memoryStream = new MemoryStream(output, true);

        byte[] helloWorld = "Hello, World!"u8.ToArray();
        byte[] msg1 = "Msg 1"u8.ToArray();
        byte[] msg2 = "Msg 2"u8.ToArray();
        byte[] msg3 = "Msg 3"u8.ToArray();
        byte[] msg4 = "Msg 4"u8.ToArray();
        byte[] innerMessage = "Inner Message"u8.ToArray();

        Benchmarks.Protos.ProtobufNet.RootMessage root = new Benchmarks.Protos.ProtobufNet.RootMessage();
        Benchmarks.Protos.ProtobufNet.TestMessage message = new Benchmarks.Protos.ProtobufNet.TestMessage();
        message.E = new List<byte[]>();
        message.F = new List<Benchmarks.Protos.ProtobufNet.SubMessage>();
        for (int j = 0; j < 200000; ++j)
        {
            root.Messages.Clear();
            message.E.Clear();
            message.F.Clear();
            message.A = ulong.MaxValue;
            message.B = long.MinValue;
            message.D = helloWorld;// "Hello, World!";
            message.E.Add(msg1);//"Msg 1");
            message.E.Add(msg2);//"Msg 2");
            message.E.Add(msg3);//"Msg 3");
            message.E.Add(msg4);//"Msg 4");

            for (int i = 0; i < 9; ++i)
            {
                Benchmarks.Protos.ProtobufNet.SubMessage subMessage = new Benchmarks.Protos.ProtobufNet.SubMessage();
                subMessage.Id = i;
                subMessage.Name = innerMessage;
                message.F.Add(subMessage);
            }
            root.Messages.Add(message);
            ProtoBuf.Serializer.Serialize(memoryStream, root);
        }
    }
}