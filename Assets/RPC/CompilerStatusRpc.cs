using Unity.Burst;
using Unity.NetCode;

[BurstCompile]
public struct CompilerStatusRpc : IRpcCommand
{
    public required FileId FileName;
    public required CompilationStatus Status;
    public required float Progress;
    public required bool IsSuccess;
    public required long CompiledVersion;
    public required long LatestVersion;
}
