using Unity.Burst;
using Unity.Entities;

[BurstCompile]
public struct TerrainFeatures : IComponentData
{
    public Entity ResourcePrefab;
}
