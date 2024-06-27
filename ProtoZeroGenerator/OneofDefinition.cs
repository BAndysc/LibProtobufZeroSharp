using System.Collections.Generic;

namespace ProtoZeroGenerator;

internal class OneofDefinition
{
    public string Name { get; set; }
    public List<FieldDefinition> Fields { get; } = new List<FieldDefinition>();
    public bool Inline { get; set; } = false;
}