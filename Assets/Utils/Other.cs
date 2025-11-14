using System;
using Unity.Burst;
using Unity.Mathematics;

public static partial class Utils
{
    [BurstCompile]
    public static byte NextAlphanumeric(this ref Unity.Mathematics.Random random) => random.NextInt(0, 2) switch
    {
        0 => (byte)random.NextInt('a', 'z'),
        1 => (byte)random.NextInt('A', 'A'),
        2 => (byte)random.NextInt('0', '9'),
        _ => throw new UnreachableException(),
    };

    [BurstCompile]
    public static float Distance(in float3 point, in float3x2 line)
    {
        float a = math.distance(line.c0, line.c1);
        float b = math.distance(line.c0, point);
        float c = math.distance(line.c1, point);
        float s = (a + b + c) / 2f;
        return 2f * math.sqrt(s * (s - a) * (s - b) * (s - c)) / a;
    }

    public static bool RayIntersectsTriangle(
        in float3 rayOrigin,
        in float3 rayVector,
        in float3x3 triangle,
        out float t)
    {
        float3 edge1 = triangle.c1 - triangle.c0;
        float3 edge2 = triangle.c2 - triangle.c0;
        float3 rayCrossE2 = math.cross(rayVector, edge2);
        float det = math.dot(edge1, rayCrossE2);

        if (det is > -float.Epsilon and < float.Epsilon)
        {
            t = default;
            return false;
        }

        float invDet = 1f / det;
        float3 s = rayOrigin - triangle.c0;
        float u = invDet * math.dot(s, rayCrossE2);

        if ((u < 0 && math.abs(u) > float.Epsilon) || (u > 1 && math.abs(u - 1) > float.Epsilon))
        {
            t = default;
            return false;
        }

        float3 sCrossE1 = math.cross(s, edge1);
        float v = invDet * math.dot(rayVector, sCrossE1);

        if ((v < 0 && math.abs(v) > float.Epsilon) || (u + v > 1 && math.abs(u + v - 1) > float.Epsilon))
        {
            t = default;
            return false;
        }

        t = invDet * math.dot(edge2, sCrossE1);
        return t > float.Epsilon;
    }
}
