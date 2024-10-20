using Unity.Entities;
using Unity.Mathematics;

public struct Projectile : IComponentData
{
    public const float Speed = 20f;

    public float3 Velocity;
}
