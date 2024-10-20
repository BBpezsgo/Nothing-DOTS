using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;

#nullable enable

[BurstCompile]
public static partial class Utils
{
    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Angle(in quaternion a, in quaternion b)
    {
        float dot = math.min(math.abs(math.dot(a, b)), 1f);
        return math.degrees(dot > 1f - 0.000001f ? 0f : math.acos(dot) * 2f);
    }

    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RotateTowards(ref quaternion from, in quaternion to, float maxDegreesDelta)
    {
        float angle = Angle(in from, in to);
        if (angle == 0f) from = to;
        else from = math.slerp(from, to, math.min(1f, maxDegreesDelta / angle));
    }

    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RotateTowards(in quaternion from, in quaternion to, float maxDegreesDelta, out quaternion result)
    {
        float angle = Angle(in from, in to);
        if (angle == 0f) result = to;
        else result = math.slerp(from, to, math.min(1f, maxDegreesDelta / angle));
    }

    /// <summary>
    /// <seealso href="https://discussions.unity.com/t/is-there-a-conversion-method-from-quaternion-to-euler/731052/39"/>
    /// </summary>
    [BurstCompile]
    public static void QuaternionToEuler(in quaternion q, out float3 euler)
    {
        const float epsilon = 1e-6f;
        const float CUTOFF = (1f - 2f * epsilon) * (1f - 2f * epsilon);
        float4 qv = q.value;
        float4 d1 = qv * qv.wwww * 2f;
        float4 d2 = qv * qv.yzxw * 2f;
        float4 d3 = qv * qv;
        float y1 = d2.y - d1.x;
        if (y1 * y1 < CUTOFF)
        {
            float x1 = d2.x + d1.z;
            float x2 = d3.y + d3.w - d3.x - d3.z;
            float z1 = d2.z + d1.y;
            float z2 = d3.z + d3.w - d3.x - d3.y;
            euler = new float3(math.atan2(x1, x2), -math.asin(y1), math.atan2(z1, z2)).yzx;
        }
        else
        {
            y1 = math.clamp(y1, -1f, 1f);
            float4 abcd = new(d2.z, d1.y, d2.y, d1.x);
            float x1 = 2f * (abcd.x * abcd.w + abcd.y * abcd.z);
            float4 x = abcd * abcd * new float4(-1f, 1f, -1f, 1f);
            float x2 = (x.x + x.y) + (x.z + x.w);
            euler = new float3(math.atan2(x1, x2), -math.asin(y1), 0f).yzx;
        }
    }
}