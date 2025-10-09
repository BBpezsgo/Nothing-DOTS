using System;
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
        return 2f * math.sqrt(s * (s - a) * (s - b) * (s - c)) / a;
    }

    public static bool ray_intersects_triangle(
        in float3 ray_origin,
        in float3 ray_vector,
        in float3x3 triangle,
        out float t)
    {
        var edge1 = triangle.c1 - triangle.c0;
        var edge2 = triangle.c2 - triangle.c0;
        var ray_cross_e2 = math.cross(ray_vector, edge2);
        float det = math.dot(edge1, ray_cross_e2);

        if (det is > (-float.Epsilon) and < float.Epsilon)
        {
            t = default;
            return false;    // This ray is parallel to this triangle.
        }

        float inv_det = 1f / det;
        float3 s = ray_origin - triangle.c0;
        float u = inv_det * math.dot(s, ray_cross_e2);

        if ((u < 0 && math.abs(u) > float.Epsilon) || (u > 1 && math.abs(u - 1) > float.Epsilon))
        {
            t = default;
            return false;
        }

        float3 s_cross_e1 = math.cross(s, edge1);
        float v = inv_det * math.dot(ray_vector, s_cross_e1);

        if ((v < 0 && math.abs(v) > float.Epsilon) || (u + v > 1 && math.abs(u + v - 1) > float.Epsilon))
        {
            t = default;
            return false;
        }

        // At this stage we can compute t to find out where the intersection point is on the line.
        t = inv_det * math.dot(edge2, s_cross_e1);

        if (t > float.Epsilon) // ray intersection
        {
            return true;
        }
        else
        {
            t = default;
            // This means that there is a line intersection but not a ray intersection.
            return false;
        }
    }

    public static float3 FlatTo3D(this float2 point, float y = 0f) => new(point.x, y, point.y);

    public static float2 FlatFrom3D(this float3 point) => new(point.x, point.z);
}
