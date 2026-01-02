using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

public struct Vehicle : IComponentData
{
    public const float MaxSpeed = 10;
    public const float SteerSpeed = 40;

    [GhostField(Quantization = 100)] public float Speed;
    [GhostField(Quantization = 100)] public float2 Input;
}
