using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct SetProcessorSourceRequestRpc : IRpcCommand
{
    public required FixedString64Bytes Source;
    public required long Version;
    public required GhostInstance Entity;
}
