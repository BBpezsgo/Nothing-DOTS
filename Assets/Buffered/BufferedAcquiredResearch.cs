using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public struct BufferedAcquiredResearch : IBufferElementData
{
    public required FixedString64Bytes Name;
}
