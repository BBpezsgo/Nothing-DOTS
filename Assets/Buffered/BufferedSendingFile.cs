using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public struct BufferedSendingFile : IBufferElementData
{
    public NetcodeEndPoint Destination;
    public int TransactionId;
    public FixedString64Bytes FileName;
    public bool AutoSendEverything;
    public double LastSentAt;
    public int TotalLength;

    public BufferedSendingFile(
        NetcodeEndPoint destination,
        int transactionId,
        FixedString64Bytes fileName,
        bool autoSendEverything,
        double lastSentAt,
        int totalLength)
    {
        Destination = destination;
        TransactionId = transactionId;
        FileName = fileName;
        AutoSendEverything = autoSendEverything;
        LastSentAt = lastSentAt;
        TotalLength = totalLength;
    }
}
