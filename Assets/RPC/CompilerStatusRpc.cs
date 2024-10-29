using Unity.Burst;
using Unity.NetCode;

[BurstCompile]
public struct CompilerStatusRpc : IRpcCommand
{
    public FileId FileName;
    public CompilationStatus Status;
    public float Progress;
    public bool IsSuccess;
    public long Version;
}
