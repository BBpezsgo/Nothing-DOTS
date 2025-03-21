global using FileChunk = Unity.Collections.FixedBytes126;

using Unity.Burst;
using Unity.NetCode;

public enum FileChunkStatus : byte
{
    OK,
    InvalidFile,
}

[BurstCompile]
public struct FileChunkResponseRpc : IRpcCommand
{
    public const int ChunkSize = 126;

    public required FileChunkStatus Status;
    public required int TransactionId;
    public required int ChunkIndex;
    public required FileChunk Data;
}
