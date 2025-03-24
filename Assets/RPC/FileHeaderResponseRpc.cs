using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

public enum FileResponseStatus
{
    OK,
    NotFound,
    NotChanged,
}

[BurstCompile]
public struct FileHeaderResponseRpc : IRpcCommand
{
    public required FileResponseStatus Status;
    public required int TransactionId;
    public required FixedString128Bytes FileName;
    public required int TotalLength;
    public required long Version;
}
