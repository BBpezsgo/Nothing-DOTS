using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

#nullable enable

[BurstCompile]
public struct SetProcessorSourceRequestRpc : IRpcCommand
{
    public FixedString64Bytes Source;
    public GhostInstance Entity;
}
