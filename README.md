# LibProtoZeroSharp

This is an experimental and highly efficient protobuf encoder designed for C#. While Protobuf is straightforward to use, it often incurs extra allocations and performance overhead. LibProtoZeroSharp aims to minimize these drawbacks by offering a zero-alloc solution (other than the buffer for the message itself).

Inspired by [ProtoZero](https://github.com/mapbox/protozero), a custom C++ protobuf encoder/decoder. LibProtoZeroSharp is a fully managed C# version.

Since this is only an experiment, currently it supports only encoding messages (and even this is limited to varint/string/submessages), however it is easy to extend it to support other protobuf features as well.

## Benchmarks

Time for some benchmarks! 

 * CanonicalProto - The official, object-oriented protobuf implementation for C#
 * ProtoZeroNative - [ProtoZero](https://github.com/mapbox/protozero) compiled with -O3 flags for comparison
 * **ProtoZeroSharp** - This project - a struct based, zero-alloc implementation
 * PerfectSerializer - an "ideal" serializer that just copies the data to the output buffer

Tested on Apple M2 Pro 12C

```
| Method           | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0       | Gen1       | Gen2      | Allocated   | Alloc Ratio |
|------------------|-----------:|----------:|----------:|------:|--------:|-----------:|-----------:|----------:|------------:|------------:|
| CanonicalProto   | 507.590 ms | 6.0581 ms | 5.0587 ms | 1.000 |    0.00 | 23000.0000 | 13000.0000 | 4000.0000 | 168998144 B |       1.000 |
| ProtoZeroNative  | 101.937 ms | 0.9745 ms | 0.8639 ms | 0.201 |    0.00 |          - |          - |         - |       147 B |       0.000 |
| ProtoZeroSharp   |  48.822 ms | 0.4310 ms | 0.4031 ms |     ? |       ? |          - |          - |         - |        67 B |           ? |
| PerfectSerializer|   1.337 ms | 0.0231 ms | 0.0205 ms | 0.003 |    0.00 |          - |          - |         - |         1 B |       0.000 |

```

The results are more than promising. Message writing is 10 times faster than the official implementation and allocates zero additional bytes compared to 168 MB in the official implementation! (For reference, the final encoded message is 52 MB). Interestingly, this even outperforms the ProtoZero C++ implementation.

To test native ProtoZero yourself, you need to download the submodule first `git submodule update --init --recursive` and run `native/build.sh` or `native/build.cmd` to build it first. On Windows, you have to execute this batch script from x64 VS Developer Console, as it invokes `cl.exe` command.

## Usage

Currently, there are no generators for encoders; you have to construct the message manually. While it's possible to implement source generators based on proto files, this project serves as a proof of concept.

```csharp
var writer = new ProtoWriter();

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

writer.Free(); // Must be called, otherwise unmanaged memory is leaked!
```


# Deserialization

```
| Method         | Mean       | Error    | StdDev   | Ratio | Gen0        | Gen1        | Gen2       | Allocated  | Alloc Ratio |
|--------------- |-----------:|---------:|---------:|------:|------------:|------------:|-----------:|-----------:|------------:|
| ProtoZeroSharp |   385.9 ms |  3.11 ms |  2.60 ms |  0.04 |           - |           - |          - |  409.34 MB |        0.33 |
| CanonicalProto | 9,932.7 ms | 53.82 ms | 44.95 ms |  1.00 | 176000.0000 | 100000.0000 | 24000.0000 | 1250.61 MB |        1.00 |
```