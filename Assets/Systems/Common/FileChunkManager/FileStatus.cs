public enum FileStatus
{
    Error,
    NotRequested,
    Receiving,
    Received,
    NotFound,
}

public static class FileStatusExtensions
{
    public static bool IsOk(this FileStatus status) =>
        status is
        FileStatus.Received;
}
