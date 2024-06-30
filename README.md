# LibProtoZeroSharp

This is an experimental and highly efficient protobuf encoder/decoder designed for C#. While Protobuf is straightforward to use, it often incurs extra allocations and performance overhead. LibProtoZeroSharp aims to minimize these drawbacks by offering a zero-additional-alloc solution (allocations only for the buffer for the message itself).

Inspired by [ProtoZero](https://github.com/mapbox/protozero), a custom C++ protobuf encoder/decoder. LibProtoZeroSharp is a fully managed C# version.

Please note this is an experimental implementation with limited protobuf feature set.

## Benchmarks

Time for some benchmarks!

### Encoding

* CanonicalProto - The official, object-oriented protobuf implementation for C#
* ProtoZeroNative - [ProtoZero](https://github.com/mapbox/protozero) compiled with -O3 flags for comparison
* **ProtoZeroSharp** - This project - a struct based, zero-alloc implementation
* PerfectSerializer - an "ideal" serializer that just copies the data to the output buffer

Tested on Apple M2 Pro 12C

```
| Method           | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0       | Gen1       | Gen2      | Allocated     | Alloc Ratio |
|------------------|-----------:|----------:|----------:|------:|--------:|-----------:|-----------:|----------:|--------------:|------------:|
| CanonicalProto   | 507.590 ms | 6.0581 ms | 5.0587 ms | 1.000 |    0.00 | 23000.0000 | 13000.0000 | 4000.0000 | 168 998 144 B |       1.000 |
| ProtoZeroNative  | 101.937 ms | 0.9745 ms | 0.8639 ms | 0.201 |    0.00 |          - |          - |         - |         147 B |       0.000 |
| ProtoZeroSharp   |  48.822 ms | 0.4310 ms | 0.4031 ms |     ? |       ? |          - |          - |         - |          67 B |           ? |
| PerfectSerializer|   1.337 ms | 0.0231 ms | 0.0205 ms | 0.003 |    0.00 |          - |          - |         - |           1 B |       0.000 |

```

The results are more than promising. Message writing is 10 times faster than the official implementation and allocates zero additional bytes compared to 168 MB in the official implementation! (For reference, the final encoded message is 52 MB). Interestingly, this even outperforms the ProtoZero C++ implementation.

To test native ProtoZero yourself, you need to download the submodule first `git submodule update --init --recursive` and run `native/build.sh` or `native/build.cmd` to build it first. On Windows, you have to execute this batch script from x64 VS Developer Console, as it invokes `cl.exe` command.


### Decoding

It is possible to decode messages on the fly, without allocating any memory at all. This can be useful when you want to deserialize only some data. But that wouldn't be fair comparison against the original implementation which always deserializes the whole message.

Luckily, LibProtoZeroSharp can also deserialize the whole message into generated structures and that's the benchmark below.

```
| Method         | Mean       | Error    | StdDev   | Ratio | Gen0        | Gen1        | Gen2       | Allocated  | Alloc Ratio |
|--------------- |-----------:|---------:|---------:|------:|------------:|------------:|-----------:|-----------:|------------:|
| ProtoZeroSharp |   385.9 ms |  3.11 ms |  2.60 ms |  0.04 |           - |           - |          - |  409.34 MB |        0.33 |
| CanonicalProto | 9,932.7 ms | 53.82 ms | 44.95 ms |  1.00 | 176000.0000 | 100000.0000 | 24000.0000 | 1250.61 MB |        1.00 |
```

This is an example 150 MB protobuf message. The official implementation allocates 3 times more memory and is 25 (!) times slower. The results are even better than encoding!

## Usage

### Encoding

Currently, there are no generators for encoders; you have to construct the message manually:

```csharp
var allocator = new ArenaAllocator();
var writer = new ProtoWriter(ref allocator);

writer.AddVarInt(Message.IdFieldNumber, 50);
writer.AddVarInt(Message.OtherFieldNumber, 1);
writer.AddVarInt(Message.OtherFieldNumber, 1); // we can add the same field, for repeated fields
writer.AddBytes(Message.StringFieldNumber, "Hello world!"u8); // we can add bytes (string) messages
writer.AddString(Message.String2FieldNumber, "Hello world!"); // or a string that will be encoded as utf-8, in place

{ // additional scope added only for readability, it is not required
    writer.StartSub(Message.SubMsgFieldNumber); // start a nested message

    writer.AddVarInt(SubMessage.IdFieldNumber, 1); // now we are writing to a nested submessage
    {
        writer.StartSub(SubMessage.DoubleNestedFieldNumber); // nested messages may contain nested messages without any problems
        writer.CloseSub(); 
    }
    
    writer.CloseSub(); // remember to close the submessage! Each Start needs a corresponding Close
}

{
    writer.StartSub(Message.SubMsgFieldNumber); // nested messages can be repeated as well

    writer.CloseSub();
}

using var file = File.Open("output");
writer.WriteTo(file); // save the results

allocator.Dispose(); // Must be called, otherwise unmanaged memory is leaked!
```

### Decoding

Decoding can be done manually or using generated classes. The manual way is useful if you want to parse data into your own structures:

**Manually:**

```csharp
var reader = new ProtoReader(data); // data is a ReadOnlySpan<byte> with the encoded message
while (reader.Next())
{
    switch (reader.Tag)
    {
        case 1:
            var varint = reader.ReadVarInt(); // for int32, int64, uint32, uint64
            break;
        case 2:
            var boolean = reader.ReadBool();
            break;
        case 3:
            var text = reader.ReadString();
            break;
        case 4:
            var byteSpan = reader.ReadBytes();
            break;
        case 5:
            var single = reader.ReadFloat();
            break;
        case 6:
            var subMessage = reader.ReadMessage();
            while (subMessage.Next())
            {
                Console.WriteLine($"Tag: {subMessage.Tag} Type: {subMessage.WireType}");
                subMessage.Skip();
            }
            break;
        default:
            reader.Skip();
            break;
    }
}
```

**Using generated classes:**

1. Reference `ProtoZeroGenerator` package.
2. Add protobuf files into .csproj as AdditionalFiles tag:
```xml
<ItemGroup>
    <AdditionalFiles Include="structures.proto" />
</ItemGroup>
```
3. Use following code to read a message into a generated structure:
```csharp
var allocator = new ArenaAllocator();
var reader = new ProtoReader(bytes);

var generatedStructure = new GeneratedStructure(); // this is the name of your protobuf message you want to deserialize
generatedStructure.Read(ref reader, ref allocator);

allocator.Dispose(); // Must be called, otherwise unmanaged memory is leaked!
```

⚠️⚠️⚠️ You may **NOT** use the structure after disposing the allocator! ⚠️⚠️⚠️