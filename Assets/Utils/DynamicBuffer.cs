using System;
using Unity.Entities;

public static class DynamicBufferExtensions
{
    public static void Set<T>(this DynamicBuffer<T> buffer, int index, in T value) where T : unmanaged
    {
        buffer.AsNativeArray().AsSpan()[index] = value;
    }

    public static TSource FirstOrDefault<TSource, TClosure>(
        this DynamicBuffer<TSource> source,
        Func<TSource, TClosure, bool> predicate,
        TClosure closure)
        where TSource : unmanaged
    {
        for (int i = 0; i < source.Length; i++)
        {
            if (predicate.Invoke(source[i], closure))
            {
                return source[i];
            }
        }
        return default;
    }
}
