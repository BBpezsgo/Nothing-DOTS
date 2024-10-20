using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

#nullable enable

public struct ShootRpc : IRpcCommand
{
    public float3 Position;
    public float3 Direction;
    public int ProjectileIndex;
}
