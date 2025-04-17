using System;
using UnityEngine;

public class FileRequest : IInspect<FileRequest>
{
    public FileId File { get; }
    public AwaitableCompletionSource<RemoteFile> Task { get; }
    public IProgress<(int Current, int Total)>? Progress { get; }
    public double RequestSentAt { get; private set; }

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

    public FileRequest OnGUI(Rect rect, FileRequest value)
    {
#if UNITY_EDITOR
        bool t = GUI.enabled;
        GUI.enabled = false;
        if (value.Progress is ProgressRecord<(int Current, int Total)> progressRecord)
        {
            UnityEditor.EditorGUI.ProgressBar(rect, progressRecord.Progress.Total == 0 ? 0f : (float)progressRecord.Progress.Current / (float)progressRecord.Progress.Total, File.ToString());
        }
        else
        {
            GUI.Label(rect, File.ToString());
        }
        GUI.enabled = true;
#endif
        return value;
    }

    public void RequestSent()
    {
        RequestSentAt = DateTime.UtcNow.TimeOfDay.TotalSeconds;
    }
}
