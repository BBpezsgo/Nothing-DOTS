using Unity.Entities;
using Unity.NetCode;

public struct BuildingPlaceholder : IComponentData
{
    public Entity BuildingPrefab;
    [GhostField(Quantization = 10)] public float TotalProgress;
    [GhostField(Quantization = 10)] public float CurrentProgress;
}
