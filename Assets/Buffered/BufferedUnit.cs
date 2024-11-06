using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[BurstCompile]
public struct BufferedUnit : IBufferElementData
{
    [GhostField(SendData = false)] public Entity Prefab;
    [GhostField] public FixedString32Bytes Name;
    [GhostField] public float ProductionTime;

    public BufferedUnit(
        Entity prefab,
        FixedString32Bytes name,
        float productionTime)
    {
        Prefab = prefab;
        Name = name;
        ProductionTime = productionTime;
    }
}
