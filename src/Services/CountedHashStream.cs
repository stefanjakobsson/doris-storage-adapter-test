using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services;

/// <summary>
/// A stream that wraps another stream and calculates the number of bytes read,
/// and the SHA256 hash, of the bytes read from the underlying stream.
/// </summary>
/// <param name="underlyingStream">The underlying stream to wrap.</param>
internal class CountedHashStream(Stream underlyingStream) : Stream
{
    private readonly Stream underlyingStream = underlyingStream;
    private long bytesRead = 0;
    private readonly IncrementalHash sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    public long BytesRead => bytesRead;

    public byte[] GetHash() => sha256.GetCurrentHash();

    public override bool CanRead => underlyingStream.CanRead;
    public override bool CanSeek => underlyingStream.CanSeek;
    public override bool CanTimeout => underlyingStream.CanTimeout;
    public override bool CanWrite => underlyingStream.CanWrite;
    public override long Length => underlyingStream.Length;

    public override long Position
    {
        get => underlyingStream.Position;
        set => underlyingStream.Position = value;
    }

    public override int ReadTimeout
    {
        get => underlyingStream.ReadTimeout;
        set => underlyingStream.ReadTimeout = value;
    }

    public override int WriteTimeout
    {
        get => underlyingStream.WriteTimeout;
        set => underlyingStream.WriteTimeout = value;
    }
    public override void Close() => underlyingStream.Close();

    protected override void Dispose(bool disposing)
    {
        sha256.Dispose();
        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        sha256.Dispose();
        return underlyingStream.DisposeAsync();
    }

    public override void Flush() => underlyingStream.Flush();

    public override Task FlushAsync(CancellationToken cancellation) => underlyingStream.FlushAsync(cancellation);

    public override int Read(Span<byte> buffer)
    {
        int bytesRead = underlyingStream.Read(buffer);

        this.bytesRead += bytesRead;
        sha256.AppendData(buffer[..bytesRead]);

        return bytesRead;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = underlyingStream.Read(buffer, offset, count);

        this.bytesRead += bytesRead;
        sha256.AppendData(buffer, offset, bytesRead);

        return bytesRead;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int bytesRead = await underlyingStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

        this.bytesRead += bytesRead;
        sha256.AppendData(buffer[..bytesRead].Span);

        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var bytesRead = await underlyingStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);

        this.bytesRead += bytesRead;
        sha256.AppendData(buffer, offset, bytesRead);

        return bytesRead;
    }

    public override int ReadByte()
    {
        var result = underlyingStream.ReadByte();

        if (result > 0)
        {
            bytesRead++;
            sha256.AppendData([ (byte)result ]);
        }

        return result;
    }

    public override long Seek(long offset, SeekOrigin origin) => underlyingStream.Seek(offset, origin);

    public override void SetLength(long value) => underlyingStream.SetLength(value);

    public override void Write(ReadOnlySpan<byte> buffer) => underlyingStream.Write(buffer);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        underlyingStream.WriteAsync(buffer, cancellationToken);

    public override void Write(byte[] buffer, int offset, int count) => underlyingStream.Write(buffer, offset, count);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        underlyingStream.WriteAsync(buffer, offset, count, cancellationToken);

    public override void WriteByte(byte value) => underlyingStream.WriteByte(value);
}