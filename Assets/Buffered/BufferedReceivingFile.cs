using Unity.Collections;
using Unity.Entities;

public struct BufferedReceivingFile : IBufferElementData
{
    public int FileId;
    public FixedString64Bytes FileName;
    public int TotalLength;
    public double LastRedeivedAt;
}
