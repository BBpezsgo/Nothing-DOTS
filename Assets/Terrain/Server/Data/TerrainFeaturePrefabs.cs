using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public struct TerrainFeaturePrefabs : IComponentData
{
    public Entity ResourcePrefab;
    public Entity ObstaclePrefab;

    public FixedString32Bytes ResourcePrefabName;
    public FixedString32Bytes ObstaclePrefabName;
}
