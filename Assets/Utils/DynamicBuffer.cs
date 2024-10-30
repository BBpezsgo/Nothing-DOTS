using System;
using Unity.Burst;
using Unity.Entities;

public static class DynamicBufferExtensions
{
    public static int IndexOf<TSource>(
        this DynamicBuffer<TSource> source,
        Func<TSource, bool> predicate)
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

    public static int IndexOf<TSource, TClosure>(
        this DynamicBuffer<TSource> source,
        Func<TSource, TClosure, bool> predicate,
        TClosure closure)
        where TSource : unmanaged
    {
        for (int i = 0; i < source.Length; i++)
        {
            if (predicate.Invoke(source[i], closure))
            {
                return i;
            }
        }
        return -1;
    }

    public static TSource FirstOrDefault<TSource>(
        this DynamicBuffer<TSource> source,
        Func<TSource, bool> predicate)
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
