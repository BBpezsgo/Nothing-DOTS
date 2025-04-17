using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public struct BufferedSendingFile : IBufferElementData
{
    public required NetcodeEndPoint Destination;
    public required int TransactionId;
    public required FixedString128Bytes FileName;
    public required bool AutoSendEverything;
    public required int TotalLength;
}
