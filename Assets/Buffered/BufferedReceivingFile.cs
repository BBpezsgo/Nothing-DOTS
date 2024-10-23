using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

#nullable enable

[BurstCompile]
public struct BufferedReceivingFile : IBufferElementData
{
    public Entity Source;
    public int TransactionId;
    public FixedString64Bytes FileName;
    public int TotalLength;
    public double LastRedeivedAt;
    public long Version;

    public BufferedReceivingFile(
        Entity source,
        int transactionId,
        FixedString64Bytes fileName,
        int totalLength,
        double lastRedeivedAt,
        long version)
    {
        Source = source;
        TransactionId = transactionId;
        FileName = fileName;
        TotalLength = totalLength;
        LastRedeivedAt = lastRedeivedAt;
        Version = version;
    }
}
