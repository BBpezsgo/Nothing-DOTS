using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.NetCode;

[BurstCompile]
public struct DebugLabelRpc : IRpcCommand
{
    public required float3 Position;
    public required byte Color;
    public required FixedString32Bytes Text;
}
