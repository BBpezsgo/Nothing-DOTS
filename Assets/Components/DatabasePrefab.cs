using Unity.Collections;
using Unity.Entities;

struct DatabasePrefab : IComponentData
{
    public FixedString32Bytes Name;
}
