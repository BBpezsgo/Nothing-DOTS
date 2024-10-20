using Unity.Collections;
using Unity.NetCode;

#nullable enable

public struct FileHeaderRequestRpc : IRpcCommand
{
    public FixedString64Bytes FileName;
}
