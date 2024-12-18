using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct BuildingsResponseRpc : IRpcCommand
{
    public required FixedString32Bytes Name;
}
