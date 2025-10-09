using Unity.Burst;
using Unity.Entities;

[BurstCompile]
public struct TerrainFeaturePrefabs : IComponentData
{
    public Entity ResourcePrefab;
}
