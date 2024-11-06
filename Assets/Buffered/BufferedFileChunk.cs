using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public struct BufferedFileChunk : IBufferElementData
{
    public NetcodeEndPoint Source;
    public int TransactionId;
    public int ChunkIndex;
    public FixedBytes126 Data;

    public BufferedFileChunk(
        NetcodeEndPoint source,
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
