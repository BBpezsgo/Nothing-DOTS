using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public struct BufferedTechnologyHashOut : IBufferElementData
{
    public required FixedString64Bytes Hash;
}
