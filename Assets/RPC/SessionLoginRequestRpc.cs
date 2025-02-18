using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct SessionLoginRequestRpc : IRpcCommand
{
    public required FixedBytes16 Guid;
}
