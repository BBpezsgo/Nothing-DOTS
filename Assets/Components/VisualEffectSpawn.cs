using Unity.Entities;
using Unity.Mathematics;

public struct VisualEffectSpawn : IComponentData
{
    public int Index;
    public quaternion Rotation;
    public float3 Position;
}
