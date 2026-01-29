using System;
using System.IO;
using BinaryWriter = Unity.Entities.Serialization.BinaryWriter;

class FileBinaryWriter : BinaryWriter
{
    readonly FileStream fileStream;

    public FileBinaryWriter(string path)
    {
        fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write);
    }

    public long Position { get => fileStream.Position; set => fileStream.Position = value; }

    public void Dispose() => fileStream.Dispose();
    public unsafe void WriteBytes(void* data, int bytes) => fileStream.Write(new ReadOnlySpan<byte>(data, bytes));
}
