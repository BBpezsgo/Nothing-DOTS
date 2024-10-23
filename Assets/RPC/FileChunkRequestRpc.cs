using Unity.Burst;
using Unity.NetCode;

[BurstCompile]
public struct FileChunkRequestRpc : IRpcCommand
{
    public required int TransactionId;
    public required int ChunkIndex;
}
