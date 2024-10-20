using Unity.Collections;
using Unity.Entities;

public struct BufferedFileChunk : IBufferElementData
{
    public int FileId;
    public int ChunkIndex;
    public FixedBytes126 Data;
}
