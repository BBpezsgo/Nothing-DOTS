using Unity.Collections;
using Unity.Entities;

public struct BufferedResearchRequirement : IBufferElementData
{
    public required FixedString64Bytes Name;
}
