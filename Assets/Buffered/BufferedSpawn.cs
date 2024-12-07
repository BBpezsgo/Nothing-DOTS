using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public struct BufferedSpawn : IBufferElementData
{
    public float3 Position;
    public bool IsOccupied;

    public BufferedSpawn(float3 position, bool isOccupied)
    {
        Position = position;
        IsOccupied = isOccupied;
    }
}
