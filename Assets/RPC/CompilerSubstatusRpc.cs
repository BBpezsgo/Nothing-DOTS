using Unity.Burst;
using Unity.NetCode;

[BurstCompile]
public struct CompilerSubstatusRpc : IRpcCommand
{
    public required FileId FileName;
    public required FileId SubFileName;
    public required int CurrentProgress;
    public required int TotalProgress;
}
