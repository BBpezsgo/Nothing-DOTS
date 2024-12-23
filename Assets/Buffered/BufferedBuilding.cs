using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public struct BufferedBuilding : IBufferElementData
{
    public required FixedString32Bytes Name;
    public required Entity Prefab;
    public required Entity PlaceholderPrefab;
    public required float ConstructionTime;
    public required float RequiredResources;
    public required FixedString64Bytes RequiredResearch;
}
