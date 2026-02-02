using System;
using UnityEngine;

public class FileRequest
{
    public FileId File { get; }
    public AwaitableCompletionSource<RemoteFile> Task { get; }
    public IProgress<(int Current, int Total)>? Progress { get; }
    public double RequestSentAt { get; set; }

    public FileRequest(
        FileId file,
        AwaitableCompletionSource<RemoteFile> task,
        IProgress<(int Current, int Total)>? progress)
    {
        File = file;
        Task = task;
        Progress = progress;
        RequestSentAt = default;
    }
}
