using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[BurstCompile]
public struct BufferedResearch : IBufferElementData
{
    [GhostField] public FixedString64Bytes Name;
    [GhostField] public FixedString32Bytes Hash;
    [GhostField] public float ResearchTime;
}
