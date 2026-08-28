using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

public enum FileResponseStatus
{
    Unknown,
    /// <summary>
    /// Set on the receiving side when the file header is requested, but the file is not yet available. The client should wait for a FileHeaderResponseRpc with a different status.
    /// </summary>
    HoldOn,
    OK,
    NotFound,
    NotChanged,
    ErrorDisconnected,
    ErrorInvalidTransaction,
}

[BurstCompile]
public struct FileHeaderResponseRpc : IRpcCommand
{
    public required FileResponseStatus Status;
    public required int TransactionId;
    public required FixedString128Bytes FileName;
    public required FixedString128Bytes RemotePath;
    public required int TotalLength;
    public required long Version;
}
