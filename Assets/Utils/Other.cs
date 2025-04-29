using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;

public static partial class Utils
{
    [BurstCompile]
    public static float Distance(in float3 point, in float3x2 line)
    {
        var a = math.distance(line.c0, line.c1);
        var b = math.distance(line.c0, point);
        var c = math.distance(line.c1, point);
        var s = (a + b + c) / 2f;
        return 2f * MathF.Sqrt(s * (s - a) * (s - b) * (s - c)) / a;
    }
}