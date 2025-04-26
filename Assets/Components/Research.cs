using Unity.Collections;
using Unity.Entities;

public struct Research : IComponentData
{
    public FixedString64Bytes Name;
    public FixedBytes30 Hash;
    public float ResearchTime;
}
