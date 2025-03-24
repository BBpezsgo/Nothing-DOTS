using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct CloseFileRpc : IRpcCommand
{
    public required FixedString128Bytes FileName;
}
