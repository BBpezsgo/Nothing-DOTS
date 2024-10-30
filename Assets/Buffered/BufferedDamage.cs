using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public readonly struct BufferedDamage : IBufferElementData
{
    public readonly float Amount;
    public readonly float3 Direction;

    public BufferedDamage(float amount, float3 direction)
    {
        Amount = amount;
        Direction = direction;
    }
}
