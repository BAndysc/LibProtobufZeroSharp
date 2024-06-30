using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ProtoZeroSharp;

/// <summary>
/// A naive, low level, unmanaged map of unmanaged types TKey and TValue.
/// Doesn't do any hashing, just linear search.
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
[DebuggerTypeProxy(typeof(UnmanagedMapDebugView<,>))]
public readonly unsafe struct UnmanagedMap<TKey, TValue> where TKey : unmanaged where TValue : unmanaged
{
    public readonly int Length;
    private readonly TKey* keys;
    private readonly TValue* values;

    private static TValue defaultValue = default;

    public UnmanagedMap(int length, TKey* keys, TValue* values)
    {
        Length = length;
        this.keys = keys;
        this.values = values;
    }

    public ref readonly TValue TryGetValue(TKey key, out bool found)
    {
        for (int i = 0; i < Length; ++i)
        {
            if (Unsafe.AsRef(in keys[i]).Equals(key))
            {
                found = true;
                return ref Unsafe.AsRef(in values[i]);
            }
        }

        found = false;
        return ref defaultValue;
    }

    public void GetUnderlyingArrays(out Span<TKey> keysSpan, out Span<TValue> valuesSpan)
    {
        keysSpan = new Span<TKey>(keys, Length);
        valuesSpan = new Span<TValue>(values, Length);
    }

    public static UnmanagedMap<TKey, TValue> AllocMap<TAllocator>(int maxCount, ref TAllocator allocator) where TAllocator : unmanaged, IAllocator
    {
        var keys = allocator.Allocate<TKey>(maxCount);
        var values = allocator.Allocate<TValue>(maxCount);
        return new UnmanagedMap<TKey, TValue>(maxCount, keys, values);
    }
}