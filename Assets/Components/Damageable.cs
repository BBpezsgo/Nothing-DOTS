using Unity.Entities;
using Unity.NetCode;

public struct Damageable : IComponentData
{
    public float MaxHealth;
    public int DestroyEffect;
    [GhostField(Quantization = 10)] public float Health;
}
