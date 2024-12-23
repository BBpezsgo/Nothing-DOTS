using Unity.Entities;
using Unity.NetCode;

public struct Resource : IComponentData
{
    public const int Capacity = 5;

    public float InitialScale;
    [GhostField] public int Amount;
}
