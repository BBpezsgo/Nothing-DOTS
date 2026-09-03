using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Nerdbank.Streams;

public class RpcStream : IDuplexPipe
{
    Pipe Incoming { get; }
    Pipe Outgoing { get; }
    public FileId RemoteIdentifier { get; }
    public double RequestSentAt { get; set; }
    public int TransactionId { get; set; }
    public int SendingIndex { get; set; }
    public int ReceivingIndex { get; set; }
    bool IsCompleted { get; set; }

    public PipeReader Input => Incoming.Reader;
    public PipeWriter Output => Outgoing.Writer;

    public RpcStream(
        FileId remoteIdentifier,
        int transactionId)
    {
        RemoteIdentifier = remoteIdentifier;

        const int estimatedThroughputBytesPerTick = 4096;
        var pauseAt = estimatedThroughputBytesPerTick * 8;
        var resumeAt = estimatedThroughputBytesPerTick * 4;

        var options = new PipeOptions(
            pauseWriterThreshold: pauseAt,
            resumeWriterThreshold: resumeAt);

        Incoming = new Pipe(options);
        Outgoing = new Pipe(options);
        TransactionId = transactionId;
    }

    public int TryRead(Span<byte> destination)
    {
        if (!Incoming.Reader.TryRead(out ReadResult result)) return 0;

        ReadOnlySequence<byte> buffer = result.Buffer;
        int toCopy = (int)Math.Min(destination.Length, buffer.Length);
        buffer.Slice(0, toCopy).CopyTo(destination);

        Incoming.Reader.AdvanceTo(buffer.GetPosition(toCopy), buffer.End);
        return toCopy;
    }

    public void ReadTo(Stream destination)
    {
        ReadTo(destination.UsePipeWriter());
    }

    public void ReadTo(IBufferWriter<byte> destination)
    {
        if (!Incoming.Reader.TryRead(out ReadResult result)) return;

        destination.Write(result.Buffer);

        Incoming.Reader.AdvanceTo(result.Buffer.End, result.Buffer.End);
    }

    public async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        ReadResult result = await Incoming.Reader.ReadAsync(cancellationToken);
        ReadOnlySequence<byte> buffer = result.Buffer;

        if (buffer.IsEmpty) return 0;

        int toCopy = (int)Math.Min(destination.Length, buffer.Length);
        buffer.Slice(0, toCopy).CopyTo(destination.Span);
        Incoming.Reader.AdvanceTo(buffer.GetPosition(toCopy), buffer.End);
        return toCopy;
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        Outgoing.Writer.Write(data);
        ValueTask<FlushResult> flush = Outgoing.Writer.FlushAsync();
        if (!flush.IsCompleted) throw new InvalidOperationException("Outgoing.Writer.FlushAsync did not complete synchronously");
    }

    public void Write(ReadOnlySequence<byte> data)
    {
        Outgoing.Writer.Write(data);
        ValueTask<FlushResult> flush = Outgoing.Writer.FlushAsync();
        if (!flush.IsCompleted) throw new InvalidOperationException("Outgoing.Writer.FlushAsync did not complete synchronously");
    }

    public void Write(PipeReader source)
    {
        if (!source.TryRead(out ReadResult res)) return;

        Write(res.Buffer);

        source.AdvanceTo(res.Buffer.End, res.Buffer.End);
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        await Outgoing.Writer.WriteAsync(data, cancellationToken);
    }

    public int DrainForSend(Span<byte> chunkBuffer)
    {
        if (!Outgoing.Reader.TryRead(out ReadResult result)) return 0;

        ReadOnlySequence<byte> buffer = result.Buffer;
        int toCopy = (int)Math.Min(chunkBuffer.Length, buffer.Length);
        buffer.Slice(0, toCopy).CopyTo(chunkBuffer);
        Outgoing.Reader.AdvanceTo(buffer.GetPosition(toCopy), buffer.End);
        return toCopy;
    }

    public void FeedReceived(ReadOnlySpan<byte> chunkPayload)
    {
        Incoming.Writer.Write(chunkPayload);
        ValueTask<FlushResult> flush = Incoming.Writer.FlushAsync();
        if (!flush.IsCompleted) throw new InvalidOperationException("Incoming.Writer.FlushAsync did not complete synchronously");
    }

    public void Complete(Exception? exception = null)
    {
        if (IsCompleted) return;
        IsCompleted = true;
        Incoming.Writer.Complete(exception);
        Outgoing.Writer.Complete(exception);
    }
}
