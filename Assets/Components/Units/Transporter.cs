using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

public struct Transporter : IComponentData
{
    public const float Reach = 6f;
    public const int Capacity = 30;
    public const float LoadSpeed = 1f;

    [GhostField(Quantization = 100)] public float LoadProgress;
    [GhostField] public int CurrentLoad;
    public float3 LoadPoint;
}
