global using StreamChunk = Unity.Collections.FixedBytes126;

using Unity.Burst;
using Unity.NetCode;

[BurstCompile]
public struct StreamChunkRpc : IRpcCommand
{
    public const int MaxChunkSize = 126;

    public required int TransactionId;
    public required int ChunkIndex;
    public required int ChunkSize;
    public required StreamChunk Data;
}
