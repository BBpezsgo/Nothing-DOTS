using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

#nullable enable

[BurstCompile]
public struct FileHeaderRpc : IRpcCommand
{
    public int TransactionId;
    public FixedString64Bytes FileName;
    public int TotalLength;
    public long Version;
}
