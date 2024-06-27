namespace ProtoZeroSharp;

public struct Optional<T> where T : unmanaged
{
    public bool HasValue;
    public T Value;
}