using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

public enum FileResponseStatus
{
    Unknown,
    OK,
    NotFound,
    NotChanged,
    ErrorDisconnected,
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
