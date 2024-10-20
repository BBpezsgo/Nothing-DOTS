using Unity.Collections;
using Unity.NetCode;

#nullable enable

public struct FileChunkRpc : IRpcCommand
{
    public const int ChunkSize = 126;

    public int FileId;
    public int ChunkIndex;
    public FixedBytes126 Data;
}
