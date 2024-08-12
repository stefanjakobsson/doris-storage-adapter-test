using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services.Storage.S3;

/// <summary>
/// In order for AmazonS3Client to support multipart uploading
/// without buffering each part in memory, it needs a stream that is seekable.
/// The disadvantage of buffering is that upload part size directly influences
/// memory usage, which limits the usable part size.
///
/// StreamWrapper is a faked seekable stream that can report Length and Position.
/// We do not actually support seeking, but seeking is only used by AmazonS3Client
/// when retrying, which we have disabled.
/// </summary>
/// <param name="underlyingStream">The underlying stream to wrap.</param>
/// <param name="length">The underlying stream's length.</param>
internal class StreamWrapper(Stream underlyingStream, long length) : Stream
{
    private readonly Stream underlyingStream = underlyingStream;
    private readonly long length = length;
    private long position = 0;

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanTimeout => underlyingStream.CanTimeout;
    public override bool CanWrite => false;
    public override long Length => length;

    public override long Position
    {
        get => position;
        set => throw new NotSupportedException();
    }

    public override int ReadTimeout
    {
        get => underlyingStream.ReadTimeout;
        set => underlyingStream.ReadTimeout = value;
    }

    public override void Close() => underlyingStream.Close();

    protected override void Dispose(bool disposing) => base.Dispose(disposing);

    public override ValueTask DisposeAsync() => underlyingStream.DisposeAsync();
 
    public override void Flush() => underlyingStream.Flush();

    public override Task FlushAsync(CancellationToken cancellation) => underlyingStream.FlushAsync(cancellation);

    public override int Read(Span<byte> buffer)
    {
        int bytesRead = underlyingStream.Read(buffer);
        position += bytesRead;
        return bytesRead;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = underlyingStream.Read(buffer, offset, count);
        position += bytesRead;
        return bytesRead;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int bytesRead = await underlyingStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        position += bytesRead;
        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
#pragma warning disable IDE0079
#pragma warning disable CA1835
        var bytesRead = await underlyingStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
#pragma warning restore IDE0079
#pragma warning restore CA1835

        position += bytesRead;
        return bytesRead;
    }

    public override int ReadByte()
    {
        var result = underlyingStream.ReadByte();

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