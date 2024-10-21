using Unity.Burst;
using Unity.Collections;
using Unity.NetCode;

#nullable enable

[BurstCompile]
public struct FileHeaderRequestRpc : IRpcCommand
{
    public FixedString64Bytes FileName;
}
