using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public readonly struct BufferedBuilding : IBufferElementData
{
    public readonly Entity Prefab;
    public readonly FixedString32Bytes Name;

    public BufferedBuilding(Entity prefab, FixedString32Bytes name)
    {
        Prefab = prefab;
        Name = name;
    }
}
