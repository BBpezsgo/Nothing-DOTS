using Unity.Entities;
using Unity.NetCode;

public struct Damageable : IComponentData
{
    public float MaxHealth;
    [GhostField] public float Health;
}
