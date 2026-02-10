using Unity.Burst;
using Unity.NetCode;

[BurstCompile]
public struct CompilerStatusRequestRpc : IRpcCommand
{
    public required FileId FileName;
}
