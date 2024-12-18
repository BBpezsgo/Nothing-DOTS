using Unity.Burst;
using Unity.Entities;

[BurstCompile]
public struct BufferedReceivingFileChunk : IBufferElementData
{
    public required NetcodeEndPoint Source;
    public required int TransactionId;
    public required int ChunkIndex;
    public required FileChunk Data;
}
