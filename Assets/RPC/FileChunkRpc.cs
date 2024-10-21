using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

#nullable enable

[BurstCompile]
public struct FileChunkRpc : IRpcCommand
{
    public const int ChunkSize = 126;

    public int TransactionId;
    public int ChunkIndex;
    public FixedBytes126 Data;
}
