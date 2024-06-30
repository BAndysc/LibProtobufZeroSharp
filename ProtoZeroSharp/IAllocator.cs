namespace ProtoZeroSharp;

/// <summary>
/// Represents an interface for a memory allocator.
/// </summary>
public interface IAllocator
{
    /// <summary>
    /// Allocates a block of memory for a specified number of elements of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The unmanaged type of the elements to allocate.</typeparam>
    /// <param name="count">The number of elements to allocate. Default is 1.</param>
    /// <returns>A pointer to the allocated memory block of type <typeparamref name="T"/>.</returns>
    unsafe T* Allocate<T>(int count = 1) where T : unmanaged;

    /// <summary>
    /// Releases any resources associated with the allocator.
    /// </summary>
    void Dispose();
}