using Unity.Burst;
using Unity.Entities;

[BurstCompile]
public readonly struct BufferedProjectile : IBufferElementData
{
    public readonly Entity Prefab;

    public BufferedProjectile(Entity prefab)
    {
        Prefab = prefab;
    }
}
