using Unity.Entities;

public struct CompilerCache : IComponentData
{
    public FileId SourceFile;
    public long Version;
    public double CompileSecuedued;
    public float HotReloadAt;
    public int DownloadingFiles;
    public int DownloadedFiles;
    public bool IsSuccess;
}
