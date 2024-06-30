using System.Text;

namespace ProtoZeroSharp.Tests;

public class ProtoWriterTests
{
    [Test]
    public void Encode_Zero_Decode_Canonical()
    {
        ArenaAllocator allocator = new();
        ProtoWriter writer = new ProtoWriter(ref allocator);

        for (int j = 0; j < 1000; ++j)
        {
            writer.AddVarInt(Canonical.TestMessage.AFieldNumber, ulong.MaxValue);
            writer.AddVarInt(Canonical.TestMessage.BFieldNumber, long.MinValue);
            writer.AddBytes(Canonical.TestMessage.DFieldNumber, "Hello, World!"u8);
            writer.AddString(Canonical.TestMessage.EFieldNumber, "Msg 1");
            writer.AddBytes(Canonical.TestMessage.EFieldNumber, "Msg 2"u8);
            writer.AddBytes(Canonical.TestMessage.EFieldNumber, "Msg 3"u8);
            writer.AddBytes(Canonical.TestMessage.EFieldNumber, "Msg 4"u8);

            for (int i = 0; i < 3; ++i)
            {
                writer.StartSub(Canonical.TestMessage.FFieldNumber);

                writer.AddVarInt(Canonical.SubMessage.IdFieldNumber, i);
                writer.AddBytes(Canonical.SubMessage.NameFieldNumber, Encoding.UTF8.GetBytes($"Name {i}"));

                writer.CloseSub(false);
            }
        }

        var buffer = new byte[writer.GetTotalLength()];
        writer.CopyTo(buffer);

        allocator.Dispose();

        var msg = Canonical.TestMessage.Parser.ParseFrom(buffer);
    }

    [Test]
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(8)]
    [TestCase(9)]
    [TestCase(10)]
    public unsafe void Randomized_Read_Write(int seed)
    {
        Random r = new Random(seed);
        ArenaAllocator allocator = new();
        ProtoWriter writer = new ProtoWriter(ref allocator);
        Span<byte> tempBuffer = stackalloc byte[1000];
        for (int i = 0; i < tempBuffer.Length; ++i)
            tempBuffer[i] = (byte)'a';

        for (int i = 0; i < 1000; ++i)
        {
            writer.StartSub(Canonical.TestMessage.FFieldNumber);

            writer.AddVarInt(Canonical.SubMessage.IdFieldNumber, i);
            writer.AddBytes(Canonical.SubMessage.NameFieldNumber, tempBuffer.Slice(0, i * 1));
            writer.StartSub(Canonical.SubMessage.OneOfFieldNumber);
            var random = r.Next(0, 3);
            if (random == 0)
            {
                writer.StartSub(Canonical.OneOfMessage.ScalarFieldNumber);
                writer.AddVarInt(Canonical.MessageScalarTypes.Uint32FieldNumber, (uint)r.Next());
                writer.AddVarInt(Canonical.MessageScalarTypes.Uint64FieldNumber, (ulong)r.Next());
                writer.AddVarInt(Canonical.MessageScalarTypes.Int32FieldNumber, r.Next());
                writer.AddVarInt(Canonical.MessageScalarTypes.Int64FieldNumber, (long)r.Next());
                writer.AddFloat(Canonical.MessageScalarTypes.FloatFieldNumber, (float)r.NextDouble());
                writer.AddDouble(Canonical.MessageScalarTypes.DoubleFieldNumber, r.NextDouble());
                writer.AddBytes(Canonical.MessageScalarTypes.StrFieldNumber, "Hello, World!"u8);
                writer.AddBytes(Canonical.MessageScalarTypes.BytesFieldNumber, tempBuffer.Slice(0, r.Next(0, 1000)));
                writer.CloseSub();
            }
            else if (random == 1)
            {
                writer.StartSub(Canonical.OneOfMessage.OptionalScalarFieldNumber);
                if (r.Next(0, 2) == 0)
                    writer.AddVarInt(Canonical.MessageScalarTypes.Uint32FieldNumber, (uint)r.Next());
                if (r.Next(0, 2) == 0)
                    writer.AddVarInt(Canonical.MessageScalarTypes.Uint64FieldNumber, (ulong)r.Next());
                if (r.Next(0, 2) == 0)
                    writer.AddVarInt(Canonical.MessageScalarTypes.Int32FieldNumber, r.Next());
                if (r.Next(0, 2) == 0)
                    writer.AddVarInt(Canonical.MessageScalarTypes.Int64FieldNumber, (long)r.Next());
                if (r.Next(0, 2) == 0)
                    writer.AddFloat(Canonical.MessageScalarTypes.FloatFieldNumber, (float)r.NextDouble());
                if (r.Next(0, 2) == 0)
                    writer.AddDouble(Canonical.MessageScalarTypes.DoubleFieldNumber, r.NextDouble());
                if (r.Next(0, 2) == 0)
                    writer.AddBytes(Canonical.MessageScalarTypes.StrFieldNumber, "Hello, World!"u8);
                if (r.Next(0, 2) == 0)
                    writer.AddBytes(Canonical.MessageScalarTypes.BytesFieldNumber, tempBuffer.Slice(0, r.Next(0, 1000)));
                writer.CloseSub();
            }
            else
            {
                writer.StartSub(Canonical.OneOfMessage.RepeatedScalarFieldNumber);
                for (int j = r.Next(0, 100); j >= 0; --j)
                    writer.AddVarInt(Canonical.MessageScalarTypes.Uint32FieldNumber, (uint)r.Next());
                for (int j = r.Next(0, 100); j >= 0; --j)
                    writer.AddVarInt(Canonical.MessageScalarTypes.Uint64FieldNumber, (ulong)r.Next());
                for (int j = r.Next(0, 100); j >= 0; --j)
                    writer.AddVarInt(Canonical.MessageScalarTypes.Int32FieldNumber, r.Next());
                for (int j = r.Next(0, 100); j >= 0; --j)
                    writer.AddVarInt(Canonical.MessageScalarTypes.Int64FieldNumber, (long)r.Next());
                for (int j = r.Next(0, 100); j >= 0; --j)
                    writer.AddFloat(Canonical.MessageScalarTypes.FloatFieldNumber, (float)r.NextDouble());
                for (int j = r.Next(0, 100); j >= 0; --j)
                    writer.AddDouble(Canonical.MessageScalarTypes.DoubleFieldNumber, r.NextDouble());
                for (int j = r.Next(0, 100); j >= 0; --j)
                    writer.AddBytes(Canonical.MessageScalarTypes.StrFieldNumber, "Hello, World!"u8);
                for (int j = r.Next(0, 100); j >= 0; --j)
                    writer.AddBytes(Canonical.MessageScalarTypes.BytesFieldNumber, tempBuffer.Slice(0, r.Next(0, 1000)));
                writer.CloseSub();
            }
            writer.CloseSub();

            writer.CloseSub();
        }

        var encoded = new byte[writer.GetTotalLength()];
        writer.CopyTo(encoded);
        allocator.Dispose();

        var decoded = Canonical.TestMessage.Parser.ParseFrom(encoded);

        allocator = new ArenaAllocator();
        var protoReader = new ProtoReader(encoded);
        var decoded_zero = new Zero.TestMessage();
        decoded_zero.Read(ref protoReader, ref allocator);

        AssertEquals(decoded, in decoded_zero);

        allocator.Dispose();
    }

    private unsafe void AssertEquals(Canonical.TestMessage canonical, in Zero.TestMessage zero)
    {
        Assert.That(zero.A, Is.EqualTo(canonical.A));
        Assert.That(zero.B, Is.EqualTo(canonical.A));
        Assert.That(zero.C.HasValue, Is.EqualTo(canonical.HasC));
        if (zero.C.HasValue)
            Assert.That(zero.C.Value, Is.EqualTo(canonical.C));
        Assert.That(zero.D.HasValue, Is.EqualTo(canonical.HasD));
        if (zero.D.HasValue)
            Assert.That(zero.D.Value.ToString(), Is.EqualTo(canonical.D));
        Assert.That(zero.E.Length, Is.EqualTo(canonical.E.Count));
        for (int i = 0; i < zero.E.Length; ++i)
            Assert.That(zero.E[i].ToString(), Is.EqualTo(canonical.E[i]));
        Assert.That(zero.F.Length, Is.EqualTo(canonical.F.Count));
        for (int i = 0; i < zero.F.Length; i++)
        {
            Assert.That(zero.F[i].Id, Is.EqualTo(canonical.F[i].Id));
            Assert.That(zero.F[i].Name.HasValue, Is.EqualTo(canonical.F[i].Name != null));
            if (zero.F[i].Name.HasValue)
                Assert.That(zero.F[i].Name.Value.ToString(), Is.EqualTo(canonical.F[i].Name));
            Assert.That((int)zero.F[i].OneOf->TypeCase, Is.EqualTo((int)canonical.F[i].OneOf.TypeCase));
            if (zero.F[i].OneOf->TypeCase == Zero.OneOfMessage.TypeOneofCase.Scalar)
            {
                ref var zeroScalar = ref zero.F[i].OneOf->Scalar;
                var canonicalScalar = canonical.F[i].OneOf.Scalar;
                Assert.That(zeroScalar.Uint32, Is.EqualTo(canonicalScalar.Uint32));
                Assert.That(zeroScalar.Uint64, Is.EqualTo(canonicalScalar.Uint64));
                Assert.That(zeroScalar.Int32, Is.EqualTo(canonicalScalar.Int32));
                Assert.That(zeroScalar.Int64, Is.EqualTo(canonicalScalar.Int64));
                Assert.That(zeroScalar.Float, Is.EqualTo(canonicalScalar.Float));
                Assert.That(zeroScalar.Double, Is.EqualTo(canonicalScalar.Double));
                Assert.That(zeroScalar.Str.ToString(), Is.EqualTo(canonicalScalar.Str));
                Assert.That(zeroScalar.Bytes.Length, Is.EqualTo(canonicalScalar.Bytes.Length));
                for (int j = 0; j < zeroScalar.Bytes.Length; ++j)
                    Assert.That(zeroScalar.Bytes[j], Is.EqualTo(canonicalScalar.Bytes[j]));
            }
            else if (zero.F[i].OneOf->TypeCase == Zero.OneOfMessage.TypeOneofCase.OptionalScalar)
            {
                ref var zeroScalar = ref zero.F[i].OneOf->OptionalScalar;
                var canonicalScalar = canonical.F[i].OneOf.OptionalScalar;
                Assert.That(zeroScalar.Uint32.HasValue, Is.EqualTo(canonicalScalar.HasUint32));
                if (zeroScalar.Uint32.HasValue)
                    Assert.That(zeroScalar.Uint32.Value, Is.EqualTo(canonicalScalar.Uint32));
                Assert.That(zeroScalar.Uint64.HasValue, Is.EqualTo(canonicalScalar.HasUint64));
                if (zeroScalar.Uint64.HasValue)
                    Assert.That(zeroScalar.Uint64.Value, Is.EqualTo(canonicalScalar.Uint64));
                Assert.That(zeroScalar.Int32.HasValue, Is.EqualTo(canonicalScalar.HasInt32));
                if (zeroScalar.Int32.HasValue)
                    Assert.That(zeroScalar.Int32.Value, Is.EqualTo(canonicalScalar.Int32));
                Assert.That(zeroScalar.Int64.HasValue, Is.EqualTo(canonicalScalar.HasInt64));
                if (zeroScalar.Int64.HasValue)
                    Assert.That(zeroScalar.Int64.Value, Is.EqualTo(canonicalScalar.Int64));
                Assert.That(zeroScalar.Float.HasValue, Is.EqualTo(canonicalScalar.HasFloat));
                if (zeroScalar.Float.HasValue)
                    Assert.That(zeroScalar.Float.Value, Is.EqualTo(canonicalScalar.Float));
                Assert.That(zeroScalar.Double.HasValue, Is.EqualTo(canonicalScalar.HasDouble));
                if (zeroScalar.Double.HasValue)
                    Assert.That(zeroScalar.Double.Value, Is.EqualTo(canonicalScalar.Double));
                Assert.That(zeroScalar.Str.HasValue, Is.EqualTo(canonicalScalar.HasStr));
                if (zeroScalar.Str.HasValue)
                    Assert.That(zeroScalar.Str.Value.ToString(), Is.EqualTo(canonicalScalar.Str));
                Assert.That(zeroScalar.Bytes.HasValue, Is.EqualTo(canonicalScalar.HasBytes));
                if (zeroScalar.Bytes.HasValue)
                {
                    Assert.That(zeroScalar.Bytes.Value.Length, Is.EqualTo(canonicalScalar.Bytes.Length));
                    for (int j = 0; j < zeroScalar.Bytes.Value.Length; ++j)
                        Assert.That(zeroScalar.Bytes.Value[j], Is.EqualTo(canonicalScalar.Bytes[j]));
                }
            }
            else if (zero.F[i].OneOf->TypeCase == Zero.OneOfMessage.TypeOneofCase.RepeatedScalar)
            {
                ref var zeroScalar = ref zero.F[i].OneOf->RepeatedScalar;
                var canonicalScalar = canonical.F[i].OneOf.RepeatedScalar;
                Assert.That(zeroScalar.Uint32.Length, Is.EqualTo(canonicalScalar.Uint32.Count));
                for (int j = 0; j < zeroScalar.Uint32.Length; ++j)
                    Assert.That(zeroScalar.Uint32[j], Is.EqualTo(canonicalScalar.Uint32[j]));
                Assert.That(zeroScalar.Uint64.Length, Is.EqualTo(canonicalScalar.Uint64.Count));
                for (int j = 0; j < zeroScalar.Uint64.Length; ++j)
                    Assert.That(zeroScalar.Uint64[j], Is.EqualTo(canonicalScalar.Uint64[j]));
                Assert.That(zeroScalar.Int32.Length, Is.EqualTo(canonicalScalar.Int32.Count));
                for (int j = 0; j < zeroScalar.Int32.Length; ++j)
                    Assert.That(zeroScalar.Int32[j], Is.EqualTo(canonicalScalar.Int32[j]));
                Assert.That(zeroScalar.Int64.Length, Is.EqualTo(canonicalScalar.Int64.Count));
                for (int j = 0; j < zeroScalar.Int64.Length; ++j)
                    Assert.That(zeroScalar.Int64[j], Is.EqualTo(canonicalScalar.Int64[j]));
                Assert.That(zeroScalar.Float.Length, Is.EqualTo(canonicalScalar.Float.Count));
                for (int j = 0; j < zeroScalar.Float.Length; ++j)
                    Assert.That(zeroScalar.Float[j], Is.EqualTo(canonicalScalar.Float[j]));
                Assert.That(zeroScalar.Double.Length, Is.EqualTo(canonicalScalar.Double.Count));
                for (int j = 0; j < zeroScalar.Double.Length; ++j)
                    Assert.That(zeroScalar.Double[j], Is.EqualTo(canonicalScalar.Double[j]));
                Assert.That(zeroScalar.Str.Length, Is.EqualTo(canonicalScalar.Str.Count));
                for (int j = 0; j < zeroScalar.Str.Length; ++j)
                    Assert.That(zeroScalar.Str[j].ToString(), Is.EqualTo(canonicalScalar.Str[j]));
                Assert.That(zeroScalar.Bytes.Length, Is.EqualTo(canonicalScalar.Bytes.Count));
                for (int j = 0; j < zeroScalar.Bytes.Length; ++j)
                {
                    Assert.That(zeroScalar.Bytes[j].Length, Is.EqualTo(canonicalScalar.Bytes[j].Length));
                    for (int k = 0; k < zeroScalar.Bytes[j].Length; ++k)
                        Assert.That(zeroScalar.Bytes[j][k], Is.EqualTo(canonicalScalar.Bytes[j][k]));
                }
            }
        }
    }
}