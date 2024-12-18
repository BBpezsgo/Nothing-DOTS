using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct ResearchDoneRpc : IRpcCommand
{
    public required FixedString64Bytes Name;
}
