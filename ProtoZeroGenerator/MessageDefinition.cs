using System.Collections.Generic;

namespace ProtoZeroGenerator;

internal class MessageDefinition
{
    public string Name { get; set; }
    public bool GenerateEquality { get; set; }
    public List<FieldDefinition> Fields { get; } = new List<FieldDefinition>();
    public List<OneofDefinition> Oneofs { get; } = new List<OneofDefinition>();
    public List<MapDefinition> Maps { get; } = new List<MapDefinition>();
}