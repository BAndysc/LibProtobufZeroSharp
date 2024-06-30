using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Antlr4.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace ProtoZeroGenerator;

[Generator]
public class ProtobufSourceGenerator : IIncrementalGenerator
{
    public Dictionary<string, string> packageToNamespace = new Dictionary<string, string>();

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var files = context.AdditionalTextsProvider
            .Where(a => a.Path.EndsWith("proto"))
            .Select((a, c) => (Path.GetFileNameWithoutExtension(a.Path), a.GetText(c)!.ToString()));

        var compilationAndFiles = context.CompilationProvider.Combine(files.Collect());

        context.RegisterSourceOutput(compilationAndFiles, (productionContext, sourceContext) => Generate(productionContext, sourceContext));
    }

    private static bool TryGetContentOfWellKnownProto(string path, out string content)
    {
        if (path == "google/protobuf/timestamp.proto")
            content = KnownProtos.Timestamp;
        else if (path == "google/protobuf/wrappers.proto")
            content = KnownProtos.Wrappers;
        else
        {
            content = null!;
            return false;
        }

        return true;
    }

    private void Generate(SourceProductionContext context, (Compilation Left, ImmutableArray<(string filename, string content)> Right) sourceContext)
    {
        HashSet<string> imported = new();
        Queue<string> toImport = new();

        foreach (var additionalFile in sourceContext.Right)
        {
            var prepass = ExecutePrePass(additionalFile.content);
            foreach (var import in prepass.Imports)
                toImport.Enqueue(import);
        }

        while (toImport.Count > 0)
        {
            var importFile = toImport.Dequeue();
            if (!imported.Add(importFile))
                continue;

            if (!TryGetContentOfWellKnownProto(importFile, out var protoFileContent))
                protoFileContent = File.ReadAllText(importFile);

            var prepass = ExecutePrePass(protoFileContent);
            foreach (var import in prepass.Imports)
                toImport.Enqueue(import);
        }

        foreach (var additionalFile in sourceContext.Right)
        {
            string code = GenerateProtobufClasses(additionalFile.content);
            context.AddSource($"{additionalFile.filename}.g.cs", SourceText.From(code, Encoding.UTF8));
        }

        foreach (var importedFile in imported)
        {
            if (!TryGetContentOfWellKnownProto(importedFile, out var protoFileContent))
                protoFileContent = File.ReadAllText(importedFile);

            string code = GenerateProtobufClasses(protoFileContent);
            context.AddSource($"{Path.GetFileNameWithoutExtension(importedFile)}.g.cs", SourceText.From(code, Encoding.UTF8));
        }
    }

    private ProtoPrePass ExecutePrePass(string fileContent)
    {
        var inputStream = new AntlrInputStream(fileContent);
        var lexer = new Protobuf3Lexer(inputStream);
        var commonTokenStream = new CommonTokenStream(lexer);
        var parser = new Protobuf3Parser(commonTokenStream);
        var tree = parser.proto();

        var prepass = new ProtoPrePass();
        prepass.Visit(tree);

        if (prepass.Package != null && prepass.Namespace != null)
            packageToNamespace[prepass.Package] = prepass.Namespace;

        return prepass;
    }

    private string GenerateProtobufClasses(string protoFileContent)
    {
        var inputStream = new AntlrInputStream(protoFileContent);
        var lexer = new Protobuf3Lexer(inputStream);
        var commonTokenStream = new CommonTokenStream(lexer);
        var parser = new Protobuf3Parser(commonTokenStream);
        var tree = parser.proto();

        var prepass = new ProtoPrePass();
        prepass.Visit(tree);

        var visitor = new ProtobufVisitor(this, prepass.Enums);
        visitor.Visit(tree);

        var codeGenerator = new CodeGenerator();
        codeGenerator.AppendLine("#nullable enable");
        codeGenerator.AppendLine("using System;");
        codeGenerator.AppendLine("using ProtoZeroSharp;");
        codeGenerator.AppendLine("using System.Runtime.CompilerServices;");
        codeGenerator.AppendLine("using System.Runtime.InteropServices;");

        if (!string.IsNullOrEmpty(prepass.Namespace))
            codeGenerator.OpenBlock($"namespace {prepass.Namespace}");

        foreach (var enumDef in visitor.Enums)
        {
            codeGenerator.OpenBlock($"public enum {enumDef.Name}");
            foreach (var value in enumDef.Values)
                codeGenerator.AppendLine($"{value.Name} = {value.Number},");
            codeGenerator.CloseBlock();
        }

        foreach (var message in visitor.Messages)
        {
            var inherits = "";
            if (message.GenerateEquality)
                inherits = " : IEquatable<" + message.Name + ">";
            codeGenerator.OpenBlock($"public unsafe partial struct {message.Name}{inherits}");

            if (prepass.Namespace == "Google.Protobuf.WellKnownTypes" && message.Name == "Timestamp")
            {
                codeGenerator.AppendLine("private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);");
                codeGenerator.OpenBlock("private static bool IsNormalized(long seconds, int nanoseconds)");
                codeGenerator.AppendLine("return nanoseconds >= 0 && nanoseconds <= 999999999 && seconds >= -62135596800L && seconds <= 253402300799L;");
                codeGenerator.CloseBlock();
                codeGenerator.OpenBlock("public DateTime ToDateTime()");
                codeGenerator.AppendLine("if (!Timestamp.IsNormalized(this.Seconds, this.Nanos))");
                codeGenerator.AppendLine("    throw new InvalidOperationException(\"Timestamp contains invalid values: Seconds={Seconds}; Nanos={Nanos}\");");
                codeGenerator.AppendLine("return Timestamp.UnixEpoch.AddSeconds((double) this.Seconds).AddTicks((long) (this.Nanos / 100));");
                codeGenerator.CloseBlock();
            }

            foreach (var field in message.Fields)
            {
                var fieldType = field.Type.ToCSharpType(field.IsOptional, field.IsOptionalPointer);
                if (field.IsRepeated)
                    fieldType = $"UnmanagedArray<{fieldType}>";
                codeGenerator.AppendLine($"public {fieldType} {field.Name};");
            }

            foreach (var map in message.Maps)
            {
                var keyType = map.KeyType.ToCSharpType(false, false);
                var valueType = map.ValueType.ToCSharpType(false, false);
                codeGenerator.AppendLine($"public UnmanagedMap<{keyType}, {valueType}> {map.Name};");
            }

            codeGenerator.OpenBlock("public static class FieldIds");
            foreach (var field in message.Fields)
            {
                codeGenerator.AppendLine($"public const int {field.Name} = {field.Number};");
            }

            foreach (var oneof in message.Oneofs)
            {
                foreach (var field in oneof.Fields)
                {
                    codeGenerator.AppendLine($"public const int {field.Name} = {field.Number};");
                }
            }

            foreach (var map in message.Maps)
            {
                codeGenerator.AppendLine($"public const int {map.Name} = {map.Number};");
            }

            codeGenerator.CloseBlock();

            foreach (var oneof in message.Oneofs)
            {
                var oneofName = oneof.Name;
                codeGenerator.AppendLine($"public {oneofName}OneofCase {oneofName}Case;");

                if (oneof.Inline)
                {
                    codeGenerator.AppendLine($"private {oneofName}Union {oneofName}Data;");
                    codeGenerator.AppendLine("[StructLayout(LayoutKind.Explicit)]");
                    codeGenerator.OpenBlock($"private struct {oneofName}Union");
                    foreach (var type in oneof.Fields)
                    {
                        codeGenerator.AppendLine($"[FieldOffset(0)] public {type.Type.ToCSharpType(false, false)} {type.Name};");
                    }
                    codeGenerator.CloseBlock();
                }
                else
                {
                    codeGenerator.AppendLine($"private byte* {oneofName}Data;");
                }

                codeGenerator.OpenBlock($"public enum {oneofName}OneofCase");
                codeGenerator.AppendLine("None,");
                foreach (var field in oneof.Fields)
                {
                    codeGenerator.AppendLine($"{field.Name},");
                }

                codeGenerator.CloseBlock();

                codeGenerator.OpenBlock($"public ref T {oneofName}<T>() where T : unmanaged");
                codeGenerator.AppendLine("#if DEBUG || !DEBUG");
                codeGenerator.AppendLine($"if ({oneofName}Case == {oneofName}OneofCase.None)");
                codeGenerator.AppendLine("throw new NullReferenceException();");
                foreach (var field in oneof.Fields)
                {
                    codeGenerator.AppendLine(
                        $"if ({oneofName}Case == {oneofName}OneofCase.{field.Name} && typeof(T) != typeof({field.Type.ToCSharpType(false, false)}))");
                    codeGenerator.AppendLine("throw new InvalidCastException();");
                }

                if (!oneof.Inline)
                {
                    codeGenerator.AppendLine($"if ({oneofName}Data == null)");
                    codeGenerator.AppendLine($"throw new NullReferenceException();");
                }
                codeGenerator.AppendLine("#endif");
                if (oneof.Inline)
                {
                    codeGenerator.AppendLine("#pragma warning disable CS9084");
                    codeGenerator.AppendLine($"return ref Unsafe.As<{oneofName}Union, T>(ref {oneofName}Data);");
                    codeGenerator.AppendLine("#pragma warning restore CS9084");
                }
                else
                {
                    codeGenerator.AppendLine($"return ref Unsafe.AsRef<T>({oneofName}Data);");
                }
                codeGenerator.CloseBlock();

                if (!oneof.Inline)
                {
                    codeGenerator.OpenBlock($"public ref T Alloc{oneofName}<T, TAllocator>(ref TAllocator memory) where T : unmanaged where TAllocator : unmanaged, IAllocator");
                    codeGenerator.AppendLine($"{oneofName}Data = (byte*)memory.Allocate<T>();");
                    codeGenerator.AppendLine($"return ref Unsafe.AsRef<T>({oneofName}Data);");
                    codeGenerator.CloseBlock();
                }

                foreach (var field in oneof.Fields)
                    codeGenerator.AppendLine($"public ref {field.Type.ToCSharpType(false, false)} {field.Name} => ref {oneofName}<{field.Type.ToCSharpType(false, false)}>();");
            }

            codeGenerator.OpenBlock("internal unsafe void Read<TAllocator>(ref ProtoReader reader, ref TAllocator memory) where TAllocator : unmanaged, IAllocator");

            foreach (var field in message.Fields.Where(f => f.IsRepeated))
            {
                codeGenerator.AppendLine($"int {field.Name}_count = 0;");
            }

            foreach (var map in message.Maps)
            {
                codeGenerator.AppendLine($"int {map.Name}_count = 0;");
            }

            if (message.Fields.Any(f => f.IsRepeated) || message.Maps.Count > 0)
            {
                codeGenerator.AppendLine("ProtoReader readerCopy = reader;");
                codeGenerator.OpenBlock("while (readerCopy.Next())");
                codeGenerator.OpenBlock("switch (readerCopy.Tag)");
                foreach (var field in message.Fields.Where(f => f.IsRepeated))
                {
                    codeGenerator.OpenBlock($"case FieldIds.{field.Name}:");

                    if (field.Type.CanBePacked())
                    {
                        codeGenerator.OpenBlock("if (readerCopy.WireType == ProtoWireType.Length)");
                        codeGenerator.AppendLine("var subReader = readerCopy.ReadMessage();");
                        if (field.Type.IsFixedSize(out var fixedSize))
                        {
                            codeGenerator.AppendLine($"{field.Name}_count += subReader.Remaining / {fixedSize};");
                        }
                        else
                        {
                            codeGenerator.OpenBlock("while (subReader.Remaining > 0)");
                            codeGenerator.AppendLine("subReader.ReadVarInt();");
                            codeGenerator.AppendLine($"{field.Name}_count++;");
                            codeGenerator.CloseBlock();
                        }

                        codeGenerator.CloseBlock();
                        codeGenerator.OpenBlock("else");
                        codeGenerator.AppendLine($"{field.Name}_count++;");
                        codeGenerator.AppendLine("readerCopy.Skip();");
                        codeGenerator.CloseBlock();
                    }
                    else
                    {
                        codeGenerator.AppendLine($"{field.Name}_count++;");
                        codeGenerator.AppendLine("readerCopy.Skip();");
                    }

                    codeGenerator.AppendLine("break;").CloseBlock();
                }

                foreach (var map in message.Maps)
                {
                    codeGenerator.OpenBlock($"case FieldIds.{map.Name}:");
                    codeGenerator.AppendLine($"{map.Name}_count++;");
                    codeGenerator.AppendLine("readerCopy.Skip();");
                    codeGenerator.AppendLine("break;").CloseBlock();
                }

                codeGenerator.AppendLine("default:");
                codeGenerator.AppendLine("readerCopy.Skip();");
                codeGenerator.AppendLine("break;");
                codeGenerator.CloseBlock();
                codeGenerator.CloseBlock();
            }

            foreach (var field in message.Fields.Where(f => f.IsRepeated))
            {
                var genericType = field.Type.ToCSharpType(false, false);
                codeGenerator.AppendLine(
                    $"{field.Name} = UnmanagedArray<{genericType}>.AllocArray<TAllocator>({field.Name}_count, ref memory);");
                codeGenerator.AppendLine($"{field.Name}_count = 0;");
            }

            foreach (var map in message.Maps)
            {
                var keyType = map.KeyType.ToCSharpType(false, false);
                var valueType = map.ValueType.ToCSharpType(false, false);
                codeGenerator.AppendLine(
                    $"{map.Name} = UnmanagedMap<{keyType}, {valueType}>.AllocMap<TAllocator>({map.Name}_count, ref memory);");
                codeGenerator.AppendLine($"{map.Name}_count = 0;");
                codeGenerator.AppendLine(
                    $"{map.Name}.GetUnderlyingArrays(out var {map.Name}_keys, out var {map.Name}_values);");
            }

            codeGenerator.OpenBlock("while (reader.Next())");
            codeGenerator.OpenBlock("switch (reader.Tag)");

            string ReadType(ProtoType type, string reader)
            {
                if (type.IsEnum)
                    return $"({type.Name}){reader}.ReadVarInt()";

                return type.BuiltinType switch
                {
                    BuiltinTypes.Double => $"{reader}.ReadDouble()",
                    BuiltinTypes.Float => $"{reader}.ReadFloat()",
                    BuiltinTypes.Int32 => $"(int){reader}.ReadVarInt()",
                    BuiltinTypes.Int64 => $"(long){reader}.ReadVarInt()",
                    BuiltinTypes.UInt32 => $"(uint){reader}.ReadVarInt()",
                    BuiltinTypes.UInt64 => $"(ulong){reader}.ReadVarInt()",
                    BuiltinTypes.SInt32 => $"(int){reader}.ReadZigZag()",
                    BuiltinTypes.SInt64 => $"(long){reader}.ReadZigZag()",
                    BuiltinTypes.Fixed32 => $"(uint){reader}.ReadFixed32()",
                    BuiltinTypes.Fixed64 => $"(ulong){reader}.ReadFixed64()",
                    BuiltinTypes.Sfixed32 => $"(int){reader}.ReadFixed32()",
                    BuiltinTypes.Sfixed64 => $"(long){reader}.ReadFixed64()",
                    BuiltinTypes.Bool => $"{reader}.ReadBool()",
                    BuiltinTypes.String => $"{reader}.ReadUtf8String<TAllocator>(ref memory)",
                    BuiltinTypes.Bytes => $"{reader}.ReadBytesArray<TAllocator>(ref memory)",
                    _ => throw new NotImplementedException($"Type {type} is not supported")
                };
            }

            foreach (var field in message.Fields)
            {
                codeGenerator.OpenBlock($"case FieldIds.{field.Name}:");
                if (field.IsRepeated)
                {
                    if (field.Type.IsMessage)
                    {
                        codeGenerator.AppendLine("var subReader = reader.ReadMessage();");
                        codeGenerator.AppendLine($"{field.Name}[{field.Name}_count] = default;");
                        codeGenerator.AppendLine(
                            $"{field.Name}[{field.Name}_count++].Read<TAllocator>(ref subReader, ref memory);");
                    }
                    else
                    {
                        if (field.Type.CanBePacked())
                        {
                            codeGenerator.OpenBlock("if (reader.WireType == ProtoWireType.Length)");
                            codeGenerator.AppendLine("var subReader = reader.ReadMessage();");
                            codeGenerator.OpenBlock("while (subReader.Remaining > 0)");
                            codeGenerator.AppendLine(
                                $"{field.Name}[{field.Name}_count++] = {ReadType(field.Type, "subReader")};");
                            codeGenerator.CloseBlock();

                            codeGenerator.CloseBlock();

                            codeGenerator.AppendLine("else");
                        }

                        codeGenerator.AppendLine(
                            $"{field.Name}[{field.Name}_count++] = {ReadType(field.Type, "reader")};");
                    }
                }
                else
                {
                    if (field.Type.IsMessage)
                    {
                        codeGenerator.AppendLine("var subReader = reader.ReadMessage();");
                        if (field.IsOptional)
                        {
                            if (field.IsOptionalPointer)
                            {
                                var fieldType = field.Type.ToCSharpType(false, false);
                                codeGenerator.AppendLine($"{field.Name} = memory.Allocate<{fieldType}>();");
                                codeGenerator.AppendLine($"*{field.Name} = default;");
                                codeGenerator.AppendLine($"{field.Name}->Read<TAllocator>(ref subReader, ref memory);");
                            }
                            else
                            {
                                codeGenerator.AppendLine($"{field.Name}.Value = default;");
                                codeGenerator.AppendLine($"{field.Name}.Value.Read<TAllocator>(ref subReader, ref memory);");
                                codeGenerator.AppendLine($"{field.Name}.HasValue = true;");
                            }
                        }
                        else
                        {
                            codeGenerator.AppendLine($"{field.Name} = default;");
                            codeGenerator.AppendLine($"{field.Name}.Read<TAllocator>(ref subReader, ref memory);");
                        }
                    }
                    else
                    {
                        codeGenerator.AppendLine($"{field.Name} = {ReadType(field.Type, "reader")};");
                    }
                }

                codeGenerator.AppendLine("break;").CloseBlock();
            }

            foreach (var map in message.Maps)
            {
                codeGenerator.OpenBlock($"case FieldIds.{map.Name}:");
                codeGenerator.AppendLine($"var subReader = reader.ReadMessage();");
                codeGenerator.OpenBlock($"while (subReader.Next())");
                codeGenerator.OpenBlock("switch (subReader.Tag)");
                codeGenerator.AppendLine("case 1:");
                codeGenerator.AppendLine($"{map.Name}_keys[{map.Name}_count] = {ReadType(map.KeyType, "subReader")};");
                codeGenerator.AppendLine("break;");
                codeGenerator.AppendLine("case 2:");
                if (map.ValueType.IsMessage)
                {
                    codeGenerator.AppendLine("var subSubReader = subReader.ReadMessage();");
                    codeGenerator.AppendLine($"{map.Name}_values[{map.Name}_count] = default;");
                    codeGenerator.AppendLine(
                        $"{map.Name}_values[{map.Name}_count].Read<TAllocator>(ref subSubReader, ref memory);");
                }
                else
                {
                    codeGenerator.AppendLine(
                        $"{map.Name}_values[{map.Name}_count] = {ReadType(map.ValueType, "subReader")};");
                }

                codeGenerator.AppendLine("break;");

                codeGenerator.CloseBlock();
                codeGenerator.CloseBlock();
                codeGenerator.AppendLine($"{map.Name}_count++;");
                codeGenerator.AppendLine("break;");
                codeGenerator.CloseBlock();
            }

            // Handle oneof fields
            foreach (var oneof in message.Oneofs)
            {
                foreach (var field in oneof.Fields)
                {
                    codeGenerator.OpenBlock($"case FieldIds.{field.Name}:");
                    var oneofName = oneof.Name.ToTitleCase();
                    codeGenerator.AppendLine($"{oneofName}Case = {oneofName}OneofCase.{field.Name};");

                    if (oneof.Inline)
                    {
                        if (field.Type.IsMessage)
                        {
                            codeGenerator.AppendLine($"{oneof.Name}Data.{field.Name} = default;");
                            codeGenerator.AppendLine($"var subReader = reader.ReadMessage();");
                            codeGenerator.AppendLine($"{oneof.Name}Data.{field.Name}.Read<TAllocator>(ref subReader, ref memory);");
                        }
                        else
                        {
                            codeGenerator.AppendLine($"{oneof.Name}Data.{field.Name} = {ReadType(field.Type, "reader")};");
                        }
                    }
                    else
                    {
                        var fieldType = field.Type.ToCSharpType(false, false);
                        codeGenerator.AppendLine($"{field.Type.ToCSharpType(false, false)}* ptr = memory.Allocate<{fieldType}>();");
                        codeGenerator.AppendLine($"{oneof.Name}Data = (byte*)ptr;");
                        if (field.Type.IsMessage)
                        {
                            codeGenerator.AppendLine("var subReader = reader.ReadMessage();");
                            codeGenerator.AppendLine("*ptr = default;");
                            codeGenerator.AppendLine("ptr->Read<TAllocator>(ref subReader, ref memory);");
                        }
                        else
                        {
                            codeGenerator.AppendLine($"*ptr = {ReadType(field.Type, "reader")};");
                        }
                    }

                    codeGenerator.AppendLine("break;").CloseBlock();
                }
            }

            codeGenerator.AppendLine("default:");
            codeGenerator.AppendLine("reader.Skip();");
            codeGenerator.AppendLine("break;");
            codeGenerator.CloseBlock();
            codeGenerator.CloseBlock();

            codeGenerator.CloseBlock();

            if (!message.GenerateEquality)
                codeGenerator.AppendLine("// no equality, because it is opt out be default, set option equality=true");
            else if (message.Maps.Count > 0)
                codeGenerator.AppendLine("// no equality, because of the map field");
            else if (message.Oneofs.Any(x => !x.Inline))
                codeGenerator.AppendLine("// no equality, because of not inline one of field");
            else if (message.Fields.Any(x => x.IsRepeated))
                codeGenerator.AppendLine("// no equality, because of repeated field");
            else
            {
                List<string> toCombine = new();
                foreach (var field in message.Fields)
                {
                    toCombine.Add($"{field.Name}");
                }
                foreach (var oneOf in message.Oneofs)
                {
                    toCombine.Add($"{oneOf.Name}Case");
                    foreach (var field in oneOf.Fields)
                    {
                        toCombine.Add($"{oneOf.Name}Case == {oneOf.Name}OneofCase.{field.Name} ? {field.Name}.GetHashCode() : 0");
                    }
                }

                if (toCombine.Count > 8)
                {
                    codeGenerator.AppendLine("// no equality, because more than 8 fields and HashCode.Combine supports only 8");
                }
                else
                {
                    codeGenerator.AppendLine($"public static bool operator ==({message.Name} left, {message.Name} right) => left.Equals(right);");
                    codeGenerator.AppendLine($"public static bool operator !=({message.Name} left, {message.Name} right) => !left.Equals(right);");

                    codeGenerator.OpenBlock($"public bool Equals({message.Name} other)");
                    List<string> equalities = new();
                    foreach (var field in message.Fields)
                    {
                        equalities.Add($"{field.Name} == other.{field.Name}");
                    }
                    foreach (var oneOf in message.Oneofs)
                    {
                        List<string> inner = new();
                        foreach (var field in oneOf.Fields)
                        {
                            inner.Add($"{oneOf.Name}Case == {oneOf.Name}OneofCase.{field.Name} && {field.Name} == other.{field.Name}");
                        }
                        equalities.Add($"{oneOf.Name}Case == other.{oneOf.Name}Case && ({string.Join(" || ", inner)})");
                    }

                    codeGenerator.AppendLine($"return " + string.Join(" &&\n            ", equalities) + ";");
                    codeGenerator.CloseBlock();


                    codeGenerator.OpenBlock($"public override bool Equals(object? obj)");
                    codeGenerator.AppendLine($"return obj is {message.Name} other && Equals(other);");
                    codeGenerator.CloseBlock();


                    codeGenerator.OpenBlock($"public override int GetHashCode()");
                    codeGenerator.AppendLine($"return HashCode.Combine({string.Join(", \n            ", toCombine)});");
                    codeGenerator.CloseBlock();
                }
            }

            codeGenerator.CloseBlock();
        }

        if (!string.IsNullOrEmpty(prepass.Namespace))
            codeGenerator.CloseBlock();

        codeGenerator.AppendLine("#nullable restore");
        return codeGenerator.ToString();
    }
}