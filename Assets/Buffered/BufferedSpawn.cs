using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public struct BufferedSpawn : IBufferElementData
{
    public required float3 Position;
    public required bool IsOccupied;
}
