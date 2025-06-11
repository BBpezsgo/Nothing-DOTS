using System.IO;
using System.IO.Compression;
using UnityEngine;

public readonly struct FileData : IInspect<FileData>
{
    public readonly byte[] Data;
    public readonly long Version;

    public byte[] Decompressed
    {
        get
        {
            using MemoryStream compressedStream = new(Data);
            using GZipStream zipStream = new(compressedStream, CompressionMode.Decompress);
            using MemoryStream resultStream = new();
            zipStream.CopyTo(resultStream);
            return resultStream.ToArray();
        }
    }

    public FileData(byte[] data, long version)
    {
        Data = data;
        Version = version;
    }

    public static FileData FromLocal(string localFile, bool compress = false)
    {
        if (compress)
        {
            using MemoryStream memStream = new();
            using GZipStream gzip = new(memStream, System.IO.Compression.CompressionLevel.Optimal);
            using FileStream fileStream = File.OpenRead(localFile);
            fileStream.CopyTo(gzip);
            return new(memStream.ToArray(), File.GetLastWriteTimeUtc(localFile).Ticks);
        }
        else
        {
            return new(File.ReadAllBytes(localFile), File.GetLastWriteTimeUtc(localFile).Ticks);
        }
    }

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
