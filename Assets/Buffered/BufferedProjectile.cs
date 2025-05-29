using Unity.Burst;
using Unity.Entities;

[BurstCompile]
public struct BufferedProjectile : IBufferElementData
{
    public required Entity Prefab;
    public required float Damage;
    public required float Speed;
    public required int ImpactEffect;
}
