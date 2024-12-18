using Unity.Burst;
using Unity.Entities;

[BurstCompile]
public struct BufferedSentFileChunk : IBufferElementData
{
    public required NetcodeEndPoint Destination;
    public required int TransactionId;
    public required int ChunkIndex;
}
