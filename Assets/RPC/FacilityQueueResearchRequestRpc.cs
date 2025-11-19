using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct FacilityQueueResearchRequestRpc : IRpcCommand
{
    public required SpawnedGhost Entity;
    public required FixedString64Bytes ResearchName;
}
