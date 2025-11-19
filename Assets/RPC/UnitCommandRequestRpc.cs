using Unity.Mathematics;
using Unity.NetCode;

public struct UnitCommandRequestRpc : IRpcCommand
{
    public required SpawnedGhost Entity;
    public required int CommandId;

    public required float3 WorldPosition;
}
