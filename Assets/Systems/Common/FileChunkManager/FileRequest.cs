using System;
using UnityEngine;

public readonly struct FileRequest : IInspect<FileRequest>
{
    public readonly FileId File;
    public readonly AwaitableCompletionSource<RemoteFile> Task;
    public readonly IProgress<(int Current, int Total)>? Progress;

    public FileRequest(
        FileId file,
        AwaitableCompletionSource<RemoteFile> task,
        IProgress<(int Current, int Total)>? progress)
    {
        File = file;
        Task = task;
        Progress = progress;
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
}
