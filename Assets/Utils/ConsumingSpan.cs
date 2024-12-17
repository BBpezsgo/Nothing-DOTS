using System;
using System.Linq;

public static partial class Utils
{
    public static void ConsumeOne(this ref ReadOnlySpan<char> v)
    {
        v = v[1..];
    }

    public static bool Consume(this ref ReadOnlySpan<char> v, char value)
    {
        if (v.Length == 0) return false;
        if (v[0] != value) return false;
        v = v[1..];
        return true;
    }

    public static bool Consume(this ref ReadOnlySpan<char> v, string value)
    {
        if (v.Length == 0) return false;
        if (!v.StartsWith(value)) return false;
        v = v[value.Length..];
        return true;
    }

    public static bool ConsumeAny(this ref ReadOnlySpan<char> v, string value)
    {
        if (v.Length == 0) return false;
        if (!value.Contains(v[0])) return false;
        v = v[1..];
        return true;
    }

    public static bool ConsumeAny(this ref ReadOnlySpan<char> v, string value, out char consumed)
    {
        consumed = default;
        if (v.Length == 0) return false;
        if (!value.Contains(v[0])) return false;
        consumed = v[0];
        v = v[1..];
        return true;
    }

    public static bool ConsumeAny(this ref ReadOnlySpan<char> v, params char[] value)
    {
        if (v.Length == 0) return false;
        if (!value.Contains(v[0])) return false;
        v = v[1..];
        return true;
    }

    public static bool ConsumeInt(this ref ReadOnlySpan<char> v, out int value)
    {
        value = default;

        ReadOnlySpan<char> current = v;

        current.Consume('-');
        while (current.ConsumeAny("0123456789")) ;

        int length = v.Length - current.Length;
        if (length == 0) return false;
        if (!int.TryParse(v[..length], out value)) return false;

        v = current;
        return true;
    }

    public static bool ConsumeUInt(this ref ReadOnlySpan<char> v, out uint value)
    {
        value = default;

        ReadOnlySpan<char> current = v;

        while (current.ConsumeAny("0123456789")) ;

        int length = v.Length - current.Length;
        if (length == 0) return false;
        if (!uint.TryParse(v[..length], out value)) return false;

        v = current;
        return true;
    }
}
