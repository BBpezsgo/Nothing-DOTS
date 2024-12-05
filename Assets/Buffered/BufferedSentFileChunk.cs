using Unity.Burst;
using Unity.Entities;

[BurstCompile]
public struct BufferedSentFileChunk : IBufferElementData
{
    public NetcodeEndPoint Destination;
    public int TransactionId;
    public int ChunkIndex;

    public BufferedSentFileChunk(
        NetcodeEndPoint destination,
        int transactionId,
        int chunkIndex)
    {
        Destination = destination;
        TransactionId = transactionId;
        ChunkIndex = chunkIndex;
    }
}
