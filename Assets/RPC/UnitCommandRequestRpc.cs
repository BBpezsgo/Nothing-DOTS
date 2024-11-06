using Unity.Mathematics;
using Unity.NetCode;

public struct UnitCommandRequestRpc : IRpcCommand
{
    public required GhostInstance Entity;
    public required int CommandId;

    public required float3 WorldPosition;
}