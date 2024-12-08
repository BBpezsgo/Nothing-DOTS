using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public struct BufferedReceivingFile : IBufferElementData
{
    public FileResponseStatus Kind;
    public NetcodeEndPoint Source;
    public int TransactionId;
    public FixedString64Bytes FileName;
    public int TotalLength;
    public double LastRedeivedAt;
    public long Version;

    public BufferedReceivingFile(
        FileResponseStatus kind,
        NetcodeEndPoint source,
        int transactionId,
        FixedString64Bytes fileName,
        int totalLength,
        double lastRedeivedAt,
        long version)
    {
        Kind = kind;
        Source = source;
        TransactionId = transactionId;
        FileName = fileName;
        TotalLength = totalLength;
        LastRedeivedAt = lastRedeivedAt;
        Version = version;
    }
}
