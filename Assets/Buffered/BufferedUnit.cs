using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[BurstCompile]
public struct BufferedUnit : IBufferElementData
{
    public Entity Prefab;
    public FixedString32Bytes Name;
    public float ProductionTime;
    public FixedString64Bytes RequiredResearch;
}
