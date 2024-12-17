using Unity.Collections;
using Unity.Entities;

public struct BufferedResearchRequirement : IBufferElementData
{
    public FixedString32Bytes Name;
}
