using Unity.Entities;

public struct Pendrive : IComponentData
{
    public int Id;
    public FixedBytes1024 Data;
}
