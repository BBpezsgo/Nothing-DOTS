using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public struct BufferedSendingFile : IBufferElementData
{
    public NetcodeEndPoint Destination;
    public int TransactionId;
    public FixedString64Bytes FileName;

    public BufferedSendingFile(
        NetcodeEndPoint destination,
        int transactionId,
        FixedString64Bytes fileName)
    {
        Destination = destination;
        TransactionId = transactionId;
        FileName = fileName;
    }
}
