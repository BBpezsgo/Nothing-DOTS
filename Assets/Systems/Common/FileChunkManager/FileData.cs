using System.IO;
using System.IO.Compression;

public readonly struct FileData
{
    public readonly byte[] Data;
    public readonly long Version;

    public FileData(byte[] data, long version)
    {
        Data = data;
        Version = version;
    }

    public readonly byte[] GetDecompressed()
    {
        using MemoryStream compressedStream = new(Data);
        using GZipStream zipStream = new(compressedStream, CompressionMode.Decompress);
        using MemoryStream resultStream = new();
        zipStream.CopyTo(resultStream);
        return resultStream.ToArray();
    }

    public static FileData FromLocal(string localFile, bool compress = false)
    {
        if (compress)
        {
            using MemoryStream memStream = new();
            using GZipStream gzip = new(memStream, CompressionLevel.Optimal);
            using FileStream fileStream = File.OpenRead(localFile);
            fileStream.CopyTo(gzip);
            return new(memStream.ToArray(), File.GetLastWriteTimeUtc(localFile).Ticks);
        }
        else
        {
            return new(File.ReadAllBytes(localFile), File.GetLastWriteTimeUtc(localFile).Ticks);
        }
    }
}
