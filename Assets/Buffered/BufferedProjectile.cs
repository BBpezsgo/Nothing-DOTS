using Unity.Burst;
using Unity.Entities;

#nullable enable

[BurstCompile]
public readonly struct BufferedProjectile : IBufferElementData
{
    public readonly Entity Prefab;

    public BufferedProjectile(Entity prefab)
    {
        Prefab = prefab;
    }
}
