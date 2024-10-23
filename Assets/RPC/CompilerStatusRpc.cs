using Unity.Burst;
using Unity.NetCode;

[BurstCompile]
public struct CompilerStatusRpc : IRpcCommand
{
    public FileId FileName;
    public int DownloadingFiles;
    public int DownloadedFiles;
    public bool IsSuccess;
    public long Version;
}
