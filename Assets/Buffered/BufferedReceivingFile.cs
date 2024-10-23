using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[BurstCompile]
public struct BufferedReceivingFile : IBufferElementData
{
    public NetcodeEndPoint Source;
    public int TransactionId;
    public FixedString64Bytes FileName;
    public int TotalLength;
    public double LastRedeivedAt;
    public long Version;

    public BufferedReceivingFile(
        NetcodeEndPoint source,
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
