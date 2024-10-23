using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct FactoryQueueUnitRequestRpc : IRpcCommand
{
    public required GhostInstance FactoryEntity;
    public required FixedString32Bytes Unit;
}
