using System;
using Unity.Entities;

#nullable enable

public static class DynamicBufferExtensions
{
    public static int IndexOf<TSource>(this DynamicBuffer<TSource> source, Func<TSource, bool> predicate)
        where TSource : unmanaged
    {
        for (int i = 0; i < source.Length; i++)
        {
            if (predicate.Invoke(source[i]))
            {
                return i;
            }
        }
        return -1;
    }

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
