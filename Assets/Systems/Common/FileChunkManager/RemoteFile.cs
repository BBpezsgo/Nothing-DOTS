using UnityEngine;

public readonly struct RemoteFile : IInspect<RemoteFile>
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

    public RemoteFile OnGUI(Rect rect, RemoteFile value)
    {
#if UNITY_EDITOR
        bool t = GUI.enabled;
        GUI.enabled = false;
        GUI.Label(rect, value.Kind.ToString());
        GUI.enabled = true;
#endif
        return value;
    }
}
