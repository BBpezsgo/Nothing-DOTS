using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

public enum FileRequestMethod
{
    OnlyHeader,
    Header,
}

[BurstCompile]
public struct FileHeaderRequestRpc : IRpcCommand
{
    public required FileRequestMethod Method;
    public required FixedString64Bytes FileName;
}
