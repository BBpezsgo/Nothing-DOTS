using System;
using Unity.Burst;
using Unity.Collections;

[BurstCompile]
public static class FixedStringExtensions
{
    [BurstCompile]
    public static FormatError AppendShift<T>(ref this T fs, Unicode.Rune rune)
         where T : unmanaged, INativeList<byte>, IUTF8Bytes
    {
        int index = fs.Length;
        int num = rune.LengthInUtf8Bytes();

        if (!fs.TryResize(index + num, NativeArrayOptions.UninitializedMemory))
        {
            fs = fs.Substring(1);
            index = fs.Length;

            if (!fs.TryResize(index + num, NativeArrayOptions.UninitializedMemory))
            {
                return FormatError.Overflow;
            }
        }

        return fs.Write(ref index, rune);
    }

    [BurstCompile]
    public static FormatError AppendShift<T>(ref this T fs, in ReadOnlySpan<Unicode.Rune> runes)
         where T : unmanaged, INativeList<byte>, IUTF8Bytes
    {
        for (int i = 0; i < runes.Length; i++)
        {
            FormatError error = fs.AppendShift(runes[i]);
            if (error != FormatError.None) return error;
        }
        return FormatError.None;
    }

    [BurstCompile]
    public static FormatError AppendShift<T>(ref this T fs, in ReadOnlySpan<char> runes)
         where T : unmanaged, INativeList<byte>, IUTF8Bytes
    {
        for (int i = 0; i < runes.Length; i++)
        {
            FormatError error = fs.AppendShift((Unicode.Rune)runes[i]);
            if (error != FormatError.None) return error;
        }
        return FormatError.None;
    }
}
