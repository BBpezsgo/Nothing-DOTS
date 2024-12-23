using Unity.Entities;

public struct Unit : IComponentData
{
    public const float RadarRadius = 80f;
    public const float TransmissionRadius = 100f;

    public Entity Radar;
    public Entity Turret;
}
