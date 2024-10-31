using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public readonly struct BufferedBuilding : IBufferElementData
{
    public readonly Entity Prefab;
    public readonly Entity PlaceholderPrefab;
    public readonly FixedString32Bytes Name;
    public readonly float TotalProgress;

    public BufferedBuilding(
        Entity prefab,
        Entity placeholderPrefab,
        FixedString32Bytes name,
        float totalProgress)
    {
        Prefab = prefab;
        PlaceholderPrefab = placeholderPrefab;
        Name = name;
        TotalProgress = totalProgress;
    }
}
