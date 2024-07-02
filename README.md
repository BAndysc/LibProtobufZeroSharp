# LibProtoZeroSharp

This is an experimental highly efficient, but limited feature-set protobuf encoder/decoder designed for C#. While Protobuf is straightforward to use, it often incurs extra allocations and performance overhead. LibProtoZeroSharp aims to minimize these drawbacks by offering a zero-additional-alloc solution (allocations only for the buffer for the message itself).

Inspired by [ProtoZero](https://github.com/mapbox/protozero), a custom C++ protobuf encoder/decoder. LibProtoZeroSharp is a fully managed C# version.

Please note this is an experimental implementation with limited protobuf feature set.

## Benchmarks

Time for some benchmarks!

### Encoding

* CanonicalProto - The official, object-oriented protobuf implementation for C#
* ProtoZeroNative - [ProtoZero](https://github.com/mapbox/protozero) compiled with -O3 flags for comparison
* Protobuf.Net - [Protobuf.Net](https://github.com/protobuf-net/protobuf-net) - alternative protobuf implementation for C#
* **ProtoZeroSharp** - This project - a struct based, zero-alloc implementation~~~~
* PerfectSerializer - an "ideal" serializer that just copies the data to the output buffer

Tested on Apple M2 Pro 12C

```
| Method            | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0       | Gen1       | Gen2      | Allocated   | Alloc Ratio |
|-------------------|-----------:|----------:|----------:|------:|--------:|-----------:|-----------:|----------:|------------:|------------:|
| CanonicalProto    | 336.232 ms | 1.4800 ms | 1.3844 ms | 1.000 |    0.00 | 21000.0000 | 11000.0000 | 2000.0000 | 168996436 B |       1.000 |
| Protobuf.Net      | 115.580 ms | 0.2154 ms | 0.1909 ms | 0.344 |    0.00 |          - |          - |         - |      1331 B |       0.000 |
| ProtoZeroNative   |  99.940 ms | 0.9738 ms | 0.8632 ms | 0.297 |    0.00 |          - |          - |         - |       123 B |       0.000 |
| PerfectSerializer |   1.295 ms | 0.0045 ms | 0.0042 ms | 0.004 |    0.00 |          - |          - |         - |         1 B |       0.000 |
| ProtoZeroSharp    |  49.215 ms | 0.5472 ms | 0.5118 ms |     ? |       ? |          - |          - |         - |        67 B |           ? |
```

The results are more than promising. Message writing is 7 times faster than the official implementation and allocates zero additional bytes compared to 168 MB in the official implementation! (For reference, the final encoded message is 52 MB). Interestingly, this even outperforms the ProtoZero C++ implementation.

However, it is important to add that the most of the cost in canonical implementation comes from the message creation itself. The writing in my tests took around 100 ms (still twice as slow) and did not allocate additional memory. But it doesn't really matter because with official Protobuf you are forced to create the message anyway, so the cost is not avoidable.

To test native ProtoZero yourself, you need to download the submodule first `git submodule update --init --recursive` and run `native/build.sh` or `native/build.cmd` to build it first. On Windows, you have to execute this batch script from x64 VS Developer Console, as it invokes `cl.exe` command.


### Decoding

It is possible to decode messages on the fly, without allocating any memory at all. This can be useful when you want to deserialize only some data. But that wouldn't be fair comparison against the original implementation which always deserializes the whole message.

Luckily, LibProtoZeroSharp can also deserialize the whole message into generated structures and that's the benchmark below.

```
| Method              | Mean       | Error    | StdDev   | Median     | Ratio | RatioSD | Gen0        | Gen1       | Gen2      | Allocated  | Alloc Ratio |
|-------------------- |-----------:|---------:|---------:|-----------:|------:|--------:|------------:|-----------:|----------:|-----------:|------------:|
| CanonicalProto      | 2,888.2 ms | 57.59 ms | 94.63 ms | 2,944.9 ms |  1.00 |    0.00 | 156000.0000 | 80000.0000 | 5000.0000 | 1238.20 MB |        1.00 |
| Protobuf.Net        | 2,733.0 ms | 51.37 ms | 40.11 ms | 2,743.3 ms |  0.97 |    0.04 | 132000.0000 | 68000.0000 | 4000.0000 | 1033.85 MB |        0.83 |
| Protobuf.Net_Struct | 2,163.3 ms | 31.53 ms | 27.95 ms | 2,172.4 ms |  0.77 |    0.03 | 116000.0000 | 60000.0000 | 4000.0000 |  907.35 MB |        0.73 |
| ProtoZeroSharp      |   408.9 ms |  2.25 ms |  1.88 ms |   408.5 ms |  0.15 |    0.01 |           - |          - |         - |  429.98 MB |        0.35 |
```

This is an example 150 MB protobuf message. The official implementation allocates 3 times more memory and is 7 times slower. I've also compared the results with alternative Protobuf.net implementation. By default it is only a bit faster than the official one, however, unlike the official implementation, with Protobuf.net I was able to change some of the classes into structs, which gave some speed boost. It is still far from the performance of LibProtoZeroSharp.

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

> ⚠️⚠️⚠️ You may **NOT** use `ProtoWriter` after disposing the allocator! ⚠️⚠️⚠️

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

1. Reference `ProtoZeroGenerator` and `LibProtoZeroSharp` packages.
2. Add protobuf files into .csproj as AdditionalFiles tag (files must have .proto extension):
```xml
<ItemGroup>
    <AdditionalFiles Include="structures.proto" />
</ItemGroup>
```
3. Use the following code to read a message into a generated structure:
```csharp
var allocator = new ArenaAllocator();
var reader = new ProtoReader(bytes);

var generatedStructure = new GeneratedStructure(); // this is the name of your protobuf message you want to deserialize
generatedStructure.Read(ref reader, ref allocator);

// you can do whatever you want with generatedStructure here

allocator.Dispose(); // Must be called, otherwise unmanaged memory is leaked!
// do not use generatedStructure after disposing the allocator!
```

> ⚠️⚠️⚠️ You may **NOT** use the structure after disposing the allocator! Including any nested structures! ⚠️⚠️⚠️
> 
> Decoded structure uses memory allocated by the allocator. When the allocator is disposed, the memory is freed and the structure becomes invalid.
> You can do whatever you want with the structures as long as the allocator is alive.


## A note about the unsafe code

This library is faster than the official implementation because it uses unsafe code. This is a trade-off between performance and safety. As long as you follow the rules, you should be safe:
1. Dispose the allocator when you are done with it, to avoid memory leaks. Without calling dispose, the memory will **never** be freed.
2. **Do not use** the writer after disposing the allocator. When using `LibProtoZeroSharp.Debug` you will get managed exceptions, but in release mode, you will get segmentation faults!
3. **Do not use** the generated structures after disposing the allocator. No matter if you use debug or release mode, you will get segmentation faults!
4. You may copy the generated structures, but remember that they are just a shallow copy. And you may not use them after disposing the allocator.
5. **Do not copy** the allocator! It is a struct and copying it will lead to bugs. Always pass it by `ref`.

## Nuget

This library is available on Nuget:

```
<PackageReference Include="ProtobufZeroSharp" Version="0.5.0" />
```

This is a Release-mode library with commented out safety checks, so that it is faster, but also potential misusage (i.e. ReadMessage when the current tag is a varint) may lead to segmentation faults. Include `ProtobufZeroSharp.Debug` package for additional safety checks.

```
<PackageReference Include="ProtobufZeroSharp.Debug" Version="0.5.0" />
```

You can also include those packages depending on your configuration:

```
  <ItemGroup Condition="'$(Configuration)' == 'Debug'">
    <PackageReference Include="ProtobufZeroSharp.Debug" Version="0.5.0" />
  </ItemGroup>

  <ItemGroup Condition="'$(Configuration)' != 'Debug'">
    <PackageReference Include="ProtobufZeroSharp" Version="0.5.0" />
  </ItemGroup>
```

For the source generator, include 
```
<PackageReference Include="ProtobufZero.Generator" Version="0.5.0.1" />
```

And add your .proto files to the project as AdditionalFiles.
```xml
<ItemGroup>
    <AdditionalFiles Include="structures.proto" />
</ItemGroup>
```