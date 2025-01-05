using System.Runtime.CompilerServices;
using Unity.Burst;

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
}
