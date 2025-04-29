using Unity.Burst;
using Unity.Mathematics;
using Unity.NetCode;

[BurstCompile]
public struct DebugLineRpc : IRpcCommand
{
    public required float3x2 Position;
    public required byte Color;
}
