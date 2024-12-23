using Unity.Entities;

public struct Radar : IComponentData
{
    public const float RadarRadius = 80f;

    public Entity Transform;
}
