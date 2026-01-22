using Unity.Collections;
using Unity.Entities;

struct SavePrespawnedEntity : IComponentData
{
    // Max length: 74 "GlobalObjectId_V1-0-00000000000000000000000000000000-2147483647-2147483647"
    public FixedString128Bytes Id;
}
