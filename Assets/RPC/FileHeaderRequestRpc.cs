using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct FileHeaderRequestRpc : IRpcCommand
{
    public required FixedString64Bytes FileName;
    public required long Version;
}
