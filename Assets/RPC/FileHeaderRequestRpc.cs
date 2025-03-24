using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct FileHeaderRequestRpc : IRpcCommand
{
    public required FixedString128Bytes FileName;
    public required long Version;
}
