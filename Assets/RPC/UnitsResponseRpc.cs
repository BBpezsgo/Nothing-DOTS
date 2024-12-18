using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct UnitsResponseRpc : IRpcCommand
{
    public FixedString32Bytes Name;
}
