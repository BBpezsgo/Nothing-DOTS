using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

#nullable enable

[BurstCompile]
public struct FactoryQueueUnitRequestRpc : IRpcCommand
{
    public GhostInstance FactoryEntity;
    public FixedString32Bytes Unit;
}
