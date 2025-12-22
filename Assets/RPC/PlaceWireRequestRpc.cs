using Unity.Burst;
using Unity.NetCode;

[BurstCompile]
public struct PlaceWireRequestRpc : IRpcCommand
{
    public required SpawnedGhost A;
    public required SpawnedGhost B;
    public required bool IsRemove;
}
