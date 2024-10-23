using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public struct FileChunkRpc : IRpcCommand
{
    public const int ChunkSize = 126;

    public required int TransactionId;
    public required int ChunkIndex;
    public required FixedBytes126 Data;
}
