using System.IO;
using UnityEngine;

public readonly struct FileData : IInspect<FileData>
{
    public readonly byte[] Data;
    public readonly long Version;

    public FileData(byte[] data, long version)
    {
        Data = data;
        Version = version;
    }

    public static FileData FromLocal(string localFile)
        => new(File.ReadAllBytes(localFile), File.GetLastWriteTimeUtc(localFile).Ticks);

    public FileData OnGUI(Rect rect, FileData value)
    {
#if UNITY_EDITOR
        bool t = GUI.enabled;
        GUI.enabled = false;
        GUI.Label(rect, $"{value.Data.Length} bytes");
        GUI.enabled = t;
#endif
        return value;
    }
}
