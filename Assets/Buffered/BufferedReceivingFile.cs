using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public struct BufferedReceivingFile : IBufferElementData
{
    public required FileResponseStatus Status;

    public required NetcodeEndPoint Source;
    public required FixedString128Bytes FileName;

    public required FixedString128Bytes RemotePath;

    public required int TransactionId;
    public required int TotalLength;
    public required double LastReceivedAt;
    public required long Version;
}
