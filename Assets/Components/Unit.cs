using Unity.Entities;
using Unity.Mathematics;

public struct Unit : IComponentData
{
    public const float MaxSpeed = 10;

    public float Speed;
    public float2 Input;
}
