using Unity.Entities;

public struct Builder : IComponentData
{
    public const float TransmissionRadius = 100f;
    public const float BuildRadius = 3f;

    public Entity Turret;
}