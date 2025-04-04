using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;

[BurstCompile]
public static class Marshal
{
    [BurstCompile]
    public static unsafe TTo As<TFrom, TTo>(this TFrom from)
        where TFrom : unmanaged
        where TTo : unmanaged
        => *(TTo*)&from;

    [BurstCompile]
    public static unsafe TTo As<TFrom, TTo>(ref this TFrom from)
        where TFrom : unmanaged
        where TTo : unmanaged
        => *(TTo*)Unsafe.AsPointer(ref from);

    [BurstCompile]
    public static unsafe void GetString(nint memory, int pointer, out FixedString32Bytes @string)
        => GetString((void*)memory, pointer, out @string);

    [BurstCompile]
    public static unsafe void GetString(void* memory, int pointer, out FixedString32Bytes @string)
    {
        @string = new();
        for (int i = pointer; i < pointer + 32; i += sizeof(char))
        {
            char c = *(char*)((byte*)memory + i);
            if (c == '\0') break;
            @string.Append(c);
        }
    }
}
