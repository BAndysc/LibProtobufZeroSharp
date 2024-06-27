using System.Collections.Generic;

namespace ProtoZeroGenerator;

internal class EnumDefinition
{
    public string Name { get; set; }
    public List<EnumValue> Values { get; } = new List<EnumValue>();
}