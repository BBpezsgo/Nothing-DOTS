using Unity.Collections;
using Unity.Entities;

public struct BufferedSendingFile : IBufferElementData
{
    public int FileId;
    public FixedString64Bytes FileName;
}
