using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

public enum FileHeaderKind
{
    Ok,
    NotFound,
    OnlyHeader,
}

[BurstCompile]
public struct FileHeaderRpc : IRpcCommand
{
    public required FileHeaderKind Kind;
    public required int TransactionId;
    public required FixedString64Bytes FileName;
    public required int TotalLength;
    public required long Version;
}
