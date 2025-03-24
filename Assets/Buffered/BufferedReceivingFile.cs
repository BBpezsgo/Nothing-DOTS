using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public struct BufferedReceivingFile : IBufferElementData
{
    public required FileResponseStatus Kind;
    public required NetcodeEndPoint Source;
    public required int TransactionId;
    public required FixedString128Bytes FileName;
    public required int TotalLength;
    public required double LastRedeivedAt;
    public required long Version;
}
