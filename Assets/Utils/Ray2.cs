using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
public readonly struct Ray2
{
    public readonly float2 Start;
    public readonly float2 End;
    public readonly float2 Direction;
    public readonly uint Layer;
    [MarshalAs(UnmanagedType.U1)]
    public readonly bool ExcludeContainingBodies;

    public Ray2(float2 start, float2 end, uint layer, bool excludeContainingBodies = true)
    {
        Start = start;
        End = end;
        Direction = math.normalize(end - start);
        Layer = layer;
        ExcludeContainingBodies = excludeContainingBodies;
    }

    public float2 GetPoint(float distance) => Start + (Direction * distance);
}
