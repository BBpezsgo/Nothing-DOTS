using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct CloseStreamRpc : IRpcCommand
{
    public required FixedString128Bytes FileName;
}
