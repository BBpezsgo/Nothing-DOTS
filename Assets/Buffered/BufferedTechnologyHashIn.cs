using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public struct BufferedTechnologyHashIn : IBufferElementData
{
    public required FixedBytes30 Hash;
}
