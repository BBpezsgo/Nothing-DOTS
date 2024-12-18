using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct UnitsResponseRpc : IRpcCommand
{
    public required FixedString32Bytes Name;
}
