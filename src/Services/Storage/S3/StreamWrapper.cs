using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Storage.S3;

// In order for AmazonS3Client to support multipart uploading
// without buffering each part in memory, it needs a Stream that is seekable.
// The disadvantage of buffering is that upload part size directly influences
// memory usage, which limits the usable part size.
//
// StreamWrapper is a faked seekable stream that can report Length and Position.
// We do not actually support seeking, but seeking is only used by AmazonS3Client
// when retrying, which we have disabled.
internal class StreamWrapper(Stream inner, long length) : Stream
{
    private readonly Stream inner = inner;
    private readonly long length = length;
    private long position = 0;

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanTimeout => inner.CanTimeout;
    public override bool CanWrite => false;
    public override long Length => length;

    public override long Position
    {
        get => position;
        set => throw new NotSupportedException();
    }

    public override int ReadTimeout
    {
        get => inner.ReadTimeout;
        set => inner.ReadTimeout = value;
    }

    public override void Close() => inner.Close();

    public override void CopyTo(Stream destination, int bufferSize) => inner.CopyTo(destination, bufferSize);

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) =>
        inner.CopyToAsync(destination, bufferSize, cancellationToken);

    protected override void Dispose(bool disposing) => base.Dispose(disposing);

    public override ValueTask DisposeAsync() => inner.DisposeAsync();
 
    public override void Flush() => inner.Flush();

    public override Task FlushAsync(CancellationToken cancellation) => inner.FlushAsync(cancellation);

    public override int Read(Span<byte> buffer)
    {
        int result = inner.Read(buffer);
        position += result;
        return result;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int result = inner.Read(buffer, offset, count);
        position += result;
        return result;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var result = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        position += result;
        return result;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var result = await inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        position += result;
        return result;
    }

    public override int ReadByte()
    {
        var result = inner.ReadByte();

        if (result > 0)
        {
            position++;
        }

        return result;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}