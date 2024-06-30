#if DEBUG
using System;
#endif
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ProtoZeroSharp;

/// <summary>
/// Low level, unmanaged array of unmanaged type T.
/// Doesn't own the memory.
/// </summary>
/// <typeparam name="T"></typeparam>
[DebuggerTypeProxy(typeof(UnmanagedArrayDebugView<>))]
public readonly unsafe struct UnmanagedArray<T> where T : unmanaged
{
    public readonly int Length;
    private readonly T* data;

    public UnmanagedArray(int length, T* data)
    {
        Length = length;
        this.data = data;
    }

    public ref T this[int index]
    {
        get
        {
#if DEBUG
            if ((uint)index >= (uint)Length)
            {
                throw new IndexOutOfRangeException();
            }
#endif
            return ref Unsafe.AsRef(in data[index]);
        }
    }

    public static UnmanagedArray<T> AllocArray<TAllocator>(int maxCount, ref TAllocator allocator) where TAllocator : unmanaged, IAllocator
    {
        var data = allocator.Allocate<T>(maxCount);
        return new UnmanagedArray<T>(maxCount, data);
    }
}