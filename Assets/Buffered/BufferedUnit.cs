using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public readonly struct BufferedUnit : IBufferElementData
{
    public readonly Entity Prefab;
    public readonly FixedString32Bytes Name;

    public BufferedUnit(Entity prefab, FixedString32Bytes name)
    {
        Prefab = prefab;
        Name = name;
    }
}
