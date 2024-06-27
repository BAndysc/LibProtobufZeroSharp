using System;
using System.Collections.Generic;
using System.Linq;

namespace ProtoZeroGenerator;

internal class ProtobufVisitor : Protobuf3BaseVisitor<object>
{
    private readonly ProtobufSourceGenerator generator;
    private readonly List<string> enums;

    public ProtobufVisitor(ProtobufSourceGenerator generator, List<string> enums)
    {
        this.generator = generator;
        this.enums = enums;
    }

    public List<MessageDefinition> Messages { get; } = new List<MessageDefinition>();
    public List<EnumDefinition> Enums { get; } = new List<EnumDefinition>();

    public override object VisitExtendDef(Protobuf3Parser.ExtendDefContext context)
    {
        throw new NotImplementedException("This feature is not yet implemented in the generator");
    }

    public override object VisitServiceDef(Protobuf3Parser.ServiceDefContext context)
    {
        throw new NotImplementedException("This feature is not yet implemented in the generator");
    }

    private ProtoType Parse(string type)
    {
        if (type.TryParseBuiltinType(out var builtinType))
            return ProtoType.CreateScalar(builtinType);
        if (enums.Contains(type))
            return ProtoType.CreateEnum(type);

        foreach (var pair in generator.packageToNamespace)
        {
            var package = pair.Key;
            var @namespace = pair.Value;
            if (type.StartsWith(package + "."))
                type = @namespace + type.Substring(package.Length);
        }
        return ProtoType.CreateMessage(type);
    }

    private string GetUniqueFieldName(string messageName, string fieldName)
    {
        if (messageName == fieldName)
            return fieldName + "_";
        return fieldName;
    }

    public override object VisitMessageDef(Protobuf3Parser.MessageDefContext context)
    {
        var message = new MessageDefinition
        {
            Name = context.messageName().GetText().ToTitleCase()
        };

        foreach (var element in context.messageBody().messageElement())
        {
            if (element.field() is { } field)
            {
                var fieldTypeStr = field.type_().GetText();
                var fieldType = Parse(fieldTypeStr);
                var packedOption = field.fieldOptions()?.fieldOption()?.FirstOrDefault(o =>
                    string.Equals(o.optionName().GetText(), "packed", StringComparison.OrdinalIgnoreCase));
                message.Fields.Add(new FieldDefinition
                {
                    Name = GetUniqueFieldName(message.Name, field.fieldName().GetText().ToTitleCase()),
                    Number = ParseNumber(field.fieldNumber().GetText()),
                    Type = fieldType,
                    IsRepeated = field.fieldLabel()?.REPEATED() != null,
                    IsOptional = field.fieldLabel()?.OPTIONAL() != null,
                    IsPacked = packedOption != null
                        ? string.Equals(packedOption.constant().GetText(), "true",
                            StringComparison.OrdinalIgnoreCase)
                        : fieldType.IsPackedByDefault(),
                });
            }
            else if (element.oneof() is { } oneof)
            {
                var oneofDefinition = new OneofDefinition
                {
                    Name = oneof.oneofName().GetText().ToTitleCase()
                };

                var unionOption = oneof.optionStatement()?.FirstOrDefault(o => o.optionName().GetText() == "union");

                if (unionOption != null)
                {
                    oneofDefinition.Inline = string.Equals(unionOption.constant().GetText(), "true", StringComparison.OrdinalIgnoreCase);
                }

                foreach (var oneofField in oneof.oneofField())
                {
                    oneofDefinition.Fields.Add(new FieldDefinition
                    {
                        Name = oneofField.fieldName().GetText().ToTitleCase(),
                        Number = ParseNumber(oneofField.fieldNumber().GetText()),
                        Type = Parse(oneofField.type_().GetText())
                    });
                }

                message.Oneofs.Add(oneofDefinition);
            }
            else if (element.mapField() is { } map)
            {
                var mapDefinition = new MapDefinition()
                {
                    Name = map.mapName().GetText().ToTitleCase(),
                    Number = ParseNumber(map.fieldNumber().GetText()),
                    KeyType = Parse(map.keyType().GetText()),
                    ValueType = Parse(map.type_().GetText())
                };

                message.Maps.Add(mapDefinition);
            }
            else if (element.optionStatement() is { } option)
            {
                if (option.optionName().GetText() == "equality")
                    message.GenerateEquality =
                        string.Equals(option.constant().GetText(), "true", StringComparison.OrdinalIgnoreCase);
            }
            else if (element.emptyStatement_() != null)
                continue;
            else
                throw new NotImplementedException($"{element.GetText()} is not supported in the generator yet");
        }

        Messages.Add(message);
        return base.VisitMessageDef(context);
    }

    public override object VisitEnumDef(Protobuf3Parser.EnumDefContext context)
    {
        var enumDef = new EnumDefinition
        {
            Name = context.enumName().GetText().ToTitleCase()
        };

        foreach (var element in context.enumBody().enumElement())
        {
            var enumField = element.enumField();
            if (enumField != null)
            {
                enumDef.Values.Add(new EnumValue
                {
                    Name = enumField.ident().GetText(),
                    Number = ParseNumber(enumField.intLit().GetText())
                });
            }
        }

        Enums.Add(enumDef);
        return base.VisitEnumDef(context);
    }

    private int ParseNumber(string number)
    {
        if (number.StartsWith("0x") || number.StartsWith("0X"))
        {
            return int.Parse(number.Substring(2), System.Globalization.NumberStyles.HexNumber);
        }

        return int.Parse(number);
    }
}