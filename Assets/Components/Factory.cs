using Unity.Entities;

public struct Factory : IComponentData
{
    public const float ProductionSpeed = 1f;

    public BufferedUnit Current;
    public float CurrentProgress;
    public float TotalProgress;
}
