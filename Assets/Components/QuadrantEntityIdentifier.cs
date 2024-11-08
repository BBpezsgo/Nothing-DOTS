using Unity.Entities;

public struct QuadrantEntityIdentifier : IComponentData
{
    public bool Added;
    public uint Key;
    public uint Layer;
}
