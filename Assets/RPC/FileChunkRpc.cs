global using FileChunk = Unity.Collections.FixedBytes126;

using Unity.Burst;
using Unity.NetCode;

[BurstCompile]
public struct FileChunkRpc : IRpcCommand
{
    public const int ChunkSize = 126;

    public required int TransactionId;
    public required int ChunkIndex;
    public required FileChunk Data;
}
