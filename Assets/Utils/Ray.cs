using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
public readonly struct Ray
{
    public readonly float3 Start;
    public readonly float3 End;
    public readonly float3 Direction;
    public readonly uint Layer;
    [MarshalAs(UnmanagedType.U1)]
    public readonly bool ExcludeContainingBodies;

    public Ray(UnityEngine.Ray ray, float distance, uint layer, bool excludeContainingBodies = true)
    {
        Start = ray.origin;
        End = ray.GetPoint(distance);

#if UNITY_EDITOR && false
        if (distance == 0f)
        {
            Debug.LogWarning("Ray length is the same");
        }
#endif

        Direction = math.normalize(ray.direction);
        Layer = layer;
        ExcludeContainingBodies = excludeContainingBodies;
    }

    public Ray(float3 start, float3 end, uint layer, bool excludeContainingBodies = true)
    {
#if UNITY_EDITOR && false
        if (start.Equals(end))
        {
            Debug.LogWarning("Ray start and end point is the same");
        }
#endif

        Start = start;
        End = end;
        Direction = math.normalize(end - start);
        Layer = layer;
        ExcludeContainingBodies = excludeContainingBodies;
    }

    public float3 GetPoint(float distance) => Start + (Direction * distance);
}
