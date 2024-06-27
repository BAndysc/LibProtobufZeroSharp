namespace ProtoZeroGenerator;

internal class MapDefinition
{
    public string Name { get; set; }
    public int Number { get; set; }
    public ProtoType KeyType { get; set; }
    public ProtoType ValueType { get; set; }
}