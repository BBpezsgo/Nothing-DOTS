using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct Factory : IComponentData
{
    public const float ProductionSpeed = 1;

    public BufferedUnit Current;
    public float CurrentStartedAt;
    public float CurrentFinishAt;
}
