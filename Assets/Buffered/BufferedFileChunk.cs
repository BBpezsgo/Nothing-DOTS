using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

#nullable enable

[BurstCompile]
public struct BufferedFileChunk : IBufferElementData
{
    public Entity Source;
    public int TransactionId;
    public int ChunkIndex;
    public FixedBytes126 Data;

    public BufferedFileChunk(
        Entity source,
        int transactionId,
        int chunkIndex,
        FixedBytes126 data)
    {
        Source = source;
        TransactionId = transactionId;
        ChunkIndex = chunkIndex;
        Data = data;
    }
}
