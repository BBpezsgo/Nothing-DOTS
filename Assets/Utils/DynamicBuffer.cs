using System;
using Unity.Entities;

public static class DynamicBufferExtensions
{
    public static TSource FirstOrDefault<TSource>(this DynamicBuffer<TSource> source, Func<TSource, bool> predicate)
        where TSource : unmanaged
    {
        for (int i = 0; i < source.Length; i++)
        {
            if (predicate.Invoke(source[i]))
            {
                return source[i];
            }
        }
        return default;
    }
}
