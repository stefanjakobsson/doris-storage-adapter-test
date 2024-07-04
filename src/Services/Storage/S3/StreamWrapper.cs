using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Storage.S3;

internal class StreamWrapper(Stream underlyingStream, long length) : Stream
{
    private readonly Stream underlyingStream = underlyingStream;
    private readonly long length = length;
    private long position = 0;

    public override bool CanSeek => true;

    public override long Length => length;

    public override bool CanRead => true;

    public override bool CanWrite => false;

    public override long Position
    {
        get => position;
        set => throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void Flush() => underlyingStream.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        int result = underlyingStream.Read(buffer, offset, count);
        position += result;
        return result;
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override Task FlushAsync(CancellationToken cancellationToken) => underlyingStream.FlushAsync(cancellationToken);

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var result = await underlyingStream.ReadAsync(buffer, offset, count, cancellationToken);
        position += result;
        return result;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var result = await underlyingStream.ReadAsync(buffer, cancellationToken);
        position += result;
        return result;
    }
}