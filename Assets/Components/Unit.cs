using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

public struct Unit : IComponentData
{
    public const float MaxSpeed = 10;
    public const float SteerSpeed = 40;

    public const float RadarRadius = 80f;
    public const float TransmissionRadius = 80f;

    [GhostField(Quantization = 100)] public float Speed;
    [GhostField(Quantization = 100)] public float2 Input;
    public Entity Radar;
    public Entity Turret;
}
