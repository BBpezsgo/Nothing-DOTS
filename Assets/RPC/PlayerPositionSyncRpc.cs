using Unity.Burst;
using Unity.Mathematics;
using Unity.NetCode;

[BurstCompile]
public struct PlayerPositionSyncRpc : IRpcCommand
{
    public required float3 Position;
}
