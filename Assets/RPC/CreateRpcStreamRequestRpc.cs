using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct CreateRpcStreamRequestRpc : IRpcCommand
{
    public required FixedString128Bytes FileName;
}
