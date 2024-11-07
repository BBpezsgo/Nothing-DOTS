using Unity.Entities;
using Unity.NetCode;

public struct Factory : IComponentData
{
    public const float ProductionSpeed = 1f;

    [GhostField] public BufferedUnit Current;
    [GhostField(Quantization = 100)] public float CurrentProgress;
    [GhostField(Quantization = 100)] public float TotalProgress;
}
