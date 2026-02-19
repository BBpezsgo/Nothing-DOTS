using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public struct TerrainFeaturePrefab : IBufferElementData
{
    public int2 Quantity;
    public Entity Prefab;
    public FixedString32Bytes PrefabName;
}
