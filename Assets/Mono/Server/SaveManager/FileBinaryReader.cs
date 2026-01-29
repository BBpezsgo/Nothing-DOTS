using System;
using System.IO;
using BinaryReader = Unity.Entities.Serialization.BinaryReader;

class FileBinaryReader : BinaryReader
{
    readonly FileStream fileStream;

    public FileBinaryReader(string path)
    {
        fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
    }

    public long Position { get => fileStream.Position; set => fileStream.Position = value; }
    public bool IsEOF => fileStream.Length == fileStream.Position;

    public void Dispose() => fileStream.Dispose();
    public unsafe void ReadBytes(void* data, int bytes)
    {
        int read = fileStream.Read(new Span<byte>(data, bytes));
        if (read != bytes) throw new EndOfStreamException($"{read} != {bytes}");
    }
}
