namespace ProtoZeroGenerator;

internal class FieldDefinition
{
    public string Name { get; set; }
    public int Number { get; set; }
    public ProtoType Type { get; set; }
    public bool IsRepeated { get; set; }
    public bool IsOptional { get; set; }
    public bool IsOptionalPointer { get; set; } = true;
    public bool IsPacked { get; set; }
}