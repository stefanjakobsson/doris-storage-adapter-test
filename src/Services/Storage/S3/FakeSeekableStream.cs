using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Storage.S3;

internal class FakeSeekableStream(Stream underlyingStream, int length) : Stream
{
    private readonly Stream underlyingStream = underlyingStream;
    private readonly int length = length;

    // Delegate all operations except CanSeek and Length to underlyingStream

    public override bool CanSeek => true;

    public override long Length => length;

    public override bool CanRead => underlyingStream.CanRead;

    public override bool CanWrite => underlyingStream.CanWrite;

    public override long Position
    {
        get => underlyingStream.Position;
        set => underlyingStream.Position = value;
    }

    public override long Seek(long offset, SeekOrigin origin) => underlyingStream.Seek(offset, origin);

    public override void Flush() => underlyingStream.Flush();

    public override int Read(byte[] buffer, int offset, int count) => underlyingStream.Read(buffer, offset, count);

    public override void SetLength(long value) => underlyingStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => underlyingStream.Write(buffer, offset, count);

    public override Task FlushAsync(CancellationToken cancellationToken) => underlyingStream.FlushAsync(cancellationToken);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        underlyingStream.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        underlyingStream.ReadAsync(buffer, cancellationToken);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        underlyingStream.WriteAsync(buffer, offset, count, cancellationToken);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        underlyingStream.WriteAsync(buffer, cancellationToken);
}