using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct ResearchDoneRpc : IRpcCommand
{
    public FixedString64Bytes Name;
}
