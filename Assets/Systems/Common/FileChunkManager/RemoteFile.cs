public readonly struct RemoteFile
{
    public readonly FileResponseStatus Kind;
    public readonly FileData File;
    public readonly FileId Source;

    public RemoteFile(FileResponseStatus kind, FileData file, FileId source)
    {
        Kind = kind;
        File = file;
        Source = source;
    }
}
