using Unity.Entities;
using Unity.NetCode;

public struct Factory : IComponentData
{
    public const float ProductionSpeed = 1f;

    [GhostField] public BufferedUnit Current;
    [GhostField] public float CurrentProgress;
    [GhostField] public float TotalProgress;
}
