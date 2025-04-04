using Unity.Collections;
using Unity.Entities;

public struct Research : IComponentData
{
    public FixedString64Bytes Name;
    public FixedString32Bytes Hash;
    public float ResearchTime;
}
