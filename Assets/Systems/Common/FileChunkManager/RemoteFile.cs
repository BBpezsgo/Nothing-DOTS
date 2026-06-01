using Unity.Collections;

public readonly struct RemoteFile
{
    public readonly FileResponseStatus Status;
    public readonly FileData Data;
    public readonly FileId Source;
    public readonly FixedString128Bytes RemotePath;

    public RemoteFile(FileResponseStatus status, FileData data, FileId source, FixedString128Bytes remotePath)
    {
        Status = status;
        Data = data;
        Source = source;
        RemotePath = remotePath;
    }
}
