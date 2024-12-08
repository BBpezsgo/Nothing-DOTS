using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

public enum FileResponseStatus
{
    OK,
    NotFound,
}

[BurstCompile]
public struct FileHeaderResponseRpc : IRpcCommand
{
    public required FileResponseStatus Kind;
    public required int TransactionId;
    public required FixedString64Bytes FileName;
    public required int TotalLength;
    public required long Version;
}
