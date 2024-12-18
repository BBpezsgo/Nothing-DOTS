using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public struct BufferedSendingFile : IBufferElementData
{
    public required NetcodeEndPoint Destination;
    public required int TransactionId;
    public required FixedString64Bytes FileName;
    public required bool AutoSendEverything;
    public required double LastSentAt;
    public required int TotalLength;
}
