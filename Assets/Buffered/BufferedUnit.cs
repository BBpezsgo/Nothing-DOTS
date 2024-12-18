using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public struct BufferedUnit : IBufferElementData
{
    public required Entity Prefab;
    public required FixedString32Bytes Name;
    public required float ProductionTime;
    public required FixedString64Bytes RequiredResearch;
}
