using Unity.Entities;
using Unity.Mathematics;

public struct Projectile : IComponentData
{
    public float3 Velocity;
    public float Damage;
    public int MetalImpactEffect;
    public int DustImpactEffect;
}
