using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
public static partial class Utils
{
    [BurstCompile]
    public static float Repeat(float t, float length) => math.clamp(t - math.floor(t / length) * length, 0f, length);

    [BurstCompile]
    public static float MoveTowards(float current, float target, float maxDelta)
    {
        if (math.abs(target - current) <= maxDelta)
        {
            return target;
        }

        return current + math.sign(target - current) * maxDelta;
    }

    [BurstCompile]
    public static float DeltaAngle(float current, float target)
    {
        float num = Repeat(target - current, math.PI2);
        if (num > math.PI)
        {
            num -= math.PI2;
        }

        return num;
    }

    [BurstCompile]
    public static float MoveTowardsAngle(float current, float target, float maxDelta)
    {
        float num = DeltaAngle(current, target);
        if (0f - maxDelta < num && num < maxDelta)
        {
            return target;
        }

        target = current + num;
        return MoveTowards(current, target, maxDelta);
    }

    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static float Rad(in quaternion a, in quaternion b)
    {
        float dot = math.min(math.abs(math.dot(a, b)), 1f);
        return dot > 0.999999f ? 0f : math.acos(dot) * 2f;
    }

    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RotateTowards(ref quaternion from, in quaternion to, float maxDegreesDelta)
    {
        float rad = Rad(in from, in to);
        if (rad > 0.01f)
        {
            from = math.slerp(from, to, math.min(1f, math.radians(maxDegreesDelta) / rad));
        }
    }

    /// <summary>
    /// <seealso href="https://discussions.unity.com/t/is-there-a-conversion-method-from-quaternion-to-euler/731052/39">Source</seealso>
    /// </summary>
    public static float3 ToEuler(this in quaternion q)
    {
        ToEuler(q, out float3 v);
        return v;
    }

    /// <summary>
    /// <seealso href="https://discussions.unity.com/t/is-there-a-conversion-method-from-quaternion-to-euler/731052/39">Source</seealso>
    /// </summary>
    [BurstCompile]
    public static void ToEuler(this in quaternion q, out float3 euler)
    {
        const float epsilon = 1e-6f;
        const float cutoff = (1f - 2f * epsilon) * (1f - 2f * epsilon);
        float4 d1 = q.value * q.value.wwww * 2f;
        float4 d2 = q.value * q.value.yzxw * 2f;
        float4 d3 = q.value * q.value;
        float y1 = d2.y - d1.x;
        if (y1 * y1 < cutoff)
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
