using Unity.Burst;
using Unity.NetCode;

[BurstCompile]
public struct DestroyBuildingRpc : IRpcCommand
{
    public required SpawnedGhost Entity;
}
