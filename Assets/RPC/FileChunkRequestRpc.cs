using Unity.Burst;
using Unity.NetCode;

#nullable enable

[BurstCompile]
public struct FileChunkRequestRpc : IRpcCommand
{
    public int TransactionId;
    public int ChunkIndex;
}
