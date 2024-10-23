using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct FileHeaderRpc : IRpcCommand
{
    public required int TransactionId;
    public required FixedString64Bytes FileName;
    public required int TotalLength;
    public required long Version;
}
