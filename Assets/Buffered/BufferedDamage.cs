using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public struct BufferedDamage : IBufferElementData
{
    public required float Amount;
    public required float3 Direction;
}
