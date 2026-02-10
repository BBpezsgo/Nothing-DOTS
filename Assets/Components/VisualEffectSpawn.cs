using Unity.Entities;
using Unity.Mathematics;

public struct VisualEffectSpawn : IComponentData
{
    public int Index;
    public float3 Rotation;
    public float3 Position;
}
