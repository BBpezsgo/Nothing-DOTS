using Unity.Burst;
using Unity.Mathematics;
using Unity.NetCode;

[BurstCompile]
public struct ShootRpc : IRpcCommand
{
    public required float3 Position;
    public required float3 Direction;
    public required int ProjectileIndex;
}
