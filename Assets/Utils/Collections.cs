using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

[BurstCompile]
public static unsafe class CollectionExtensions
{
    public static UnsafeList<T> AsUnsafe<T>(this NativeArray<T> v) where T : unmanaged => !v.IsCreated ? default : new((T*)v.GetUnsafeReadOnlyPtr(), v.Length);
    public static Span<T> AsSpan<T>(this UnsafeList<T> v) where T : unmanaged => v.Ptr == default ? Span<T>.Empty : new(v.Ptr, v.Length);
    public static ReadOnlySpan<T> AsSpan<T>(this UnsafeList<T>.ReadOnly v) where T : unmanaged => v.Ptr == default ? ReadOnlySpan<T>.Empty : new(v.Ptr, v.Length);

    [BurstCompile]
    public static void AddRange<T>(this ref FixedList128Bytes<T> list, in Span<T> values) where T : unmanaged
    {
        fixed (T* ptr = values)
        {
            list.AddRange(ptr, values.Length);
        }
    }

    public static FixedList128Bytes<T> ToFixed128Bytes<T>(this in Span<T> values) where T : unmanaged
    {
        FixedList128Bytes<T> result = new();
        result.AddRange(values);
        return result;
    }

    public static void TryReplace<T>(this List<T> list, T value, T to)
    {
        int i = list.IndexOf(value);
        if (i != -1) list[i] = to;
    }
}
