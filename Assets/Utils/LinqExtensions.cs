using System;

static class LinqExtensions
{
    public static bool Any<T>(this ReadOnlySpan<T> values, Func<T, bool> predicate)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (predicate(values[i])) return true;
        }
        return false;
    }
}
