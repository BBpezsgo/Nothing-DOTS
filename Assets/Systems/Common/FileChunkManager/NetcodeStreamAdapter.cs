
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public sealed class NetcodeStreamAdapter : Stream
{
    readonly RpcStream _stream;

    public NetcodeStreamAdapter(RpcStream stream)
    {
        _stream = stream;
    }

    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;

    public override int Read(byte[] buffer, int offset, int count)
        => _stream.TryRead(buffer.AsSpan(offset, count));

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        => await _stream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);

    public override void Write(byte[] buffer, int offset, int count)
        => _stream.Write(buffer.AsSpan(offset, count));

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        => await _stream.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);

    public override void Flush() { }

    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
