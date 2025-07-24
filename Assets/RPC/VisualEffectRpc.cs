using Unity.Burst;
using Unity.Mathematics;
using Unity.NetCode;

[BurstCompile]
public struct VisualEffectRpc : IRpcCommand
{
    public required int Index;
    public required float3 Position;
    public required quaternion Rotation;
}
