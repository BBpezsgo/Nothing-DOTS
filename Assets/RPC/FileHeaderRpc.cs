using Unity.Collections;
using Unity.NetCode;

#nullable enable

public struct FileHeaderRpc : IRpcCommand
{
    public int FileId;
    public FixedString64Bytes FileName;
    public int TotalLength;
}
