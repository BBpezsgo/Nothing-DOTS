using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

public struct Rigidbody : IComponentData
{
    [GhostField(Quantization = 100)] public float3 Velocity;
    [GhostField] public bool IsEnabled;
}
