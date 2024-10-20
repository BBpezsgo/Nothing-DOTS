using Unity.NetCode;

#nullable enable

public struct FileChunkRequestRpc : IRpcCommand
{
    public int FileId;
    public int ChunkIndex;
}
