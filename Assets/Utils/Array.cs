using System;

static class ArrayExtensions
{
    public static int IndexOf<T>(this T[] values, T value)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i].Equals(value))
            { return i; }
        }
        return -1;
    }

    public static int IndexOf<T>(this T[] values, T value, Func<T, T, bool> equality)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (equality.Invoke(values[i], value))
            { return i; }
        }
        return -1;
    }
}