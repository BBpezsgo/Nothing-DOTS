using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct ResearchesResponseRpc : IRpcCommand
{
    public required FixedString64Bytes Name;
}
