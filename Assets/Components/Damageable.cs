using Unity.Entities;
using Unity.NetCode;

public struct Damageable : IComponentData
{
    public float MaxHealth;
    [GhostField(Quantization = 10)] public float Health;
}
