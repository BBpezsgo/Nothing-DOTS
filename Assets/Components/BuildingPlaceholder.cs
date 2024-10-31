using Unity.Entities;
using Unity.NetCode;

public struct BuildingPlaceholder : IComponentData
{
    public Entity BuildingPrefab;
    public float TotalProgress;
    [GhostField] public float CurrentProgress;
}
