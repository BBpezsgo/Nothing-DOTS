using System.Runtime.CompilerServices;
using Unity.Mathematics;

public static partial class Utils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Angle(in quaternion a, in quaternion b)
    {
        float dot = math.min(math.abs(math.dot(a, b)), 1f);
        return math.degrees(dot > 1f - 0.000001f ? 0f : math.acos(dot) * 2f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static quaternion RotateTowards(in quaternion from, in quaternion to, float maxDegreesDelta)
    {
        float angle = Angle(in from, in to);
        if (angle == 0f) return to;
        return math.slerp(from, to, math.min(1f, maxDegreesDelta / angle));
    }
}