using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public static unsafe class CollectionExtensions
{
    public static UnsafeList<T> AsUnsafe<T>(this NativeArray<T> v) where T : unmanaged => !v.IsCreated ? default : new((T*)v.GetUnsafeReadOnlyPtr(), v.Length);
    public static Span<T> AsSpan<T>(this UnsafeList<T> v) where T : unmanaged => v.Ptr == default ? Span<T>.Empty : new(v.Ptr, v.Length);
    public static ReadOnlySpan<T> AsSpan<T>(this UnsafeList<T>.ReadOnly v) where T : unmanaged => v.Ptr == default ? ReadOnlySpan<T>.Empty : new(v.Ptr, v.Length);
}
