using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services.Implementation.Storage.InMemory;

internal sealed class InMemoryStorageService(InMemoryStorage storage) : IStorageService
{
    private readonly InMemoryStorage storage = storage;

    public async Task<StorageFileBaseMetadata> Store(
        string filePath,
        Stream data,
        long size,
        string? contentType,
        CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        await data.CopyToAsync(memoryStream, cancellationToken);
        var byteArray = memoryStream.ToArray();

        return storage
            .AddOrUpdate(filePath, byteArray, contentType)
            .Metadata;
    }

    public Task Delete(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        storage.Remove(filePath);
        return Task.CompletedTask;
    }

    public Task<StorageFileMetadata?> GetMetadata(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (storage.TryGet(filePath, out var file))
        {
            return Task.FromResult<StorageFileMetadata?>(file.Metadata);
        }

        return Task.FromResult<StorageFileMetadata?>(null);
    }

    public Task<StorageFileData?> GetData(string filePath, StorageByteRange? byteRange, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (storage.TryGet(filePath, out var file))
        {
            Stream stream = new MemoryStream(file.Data);

            if (byteRange != null)
            {
                stream = StreamHelpers.CreateByteRangeStream(stream, byteRange);
            }

            return Task.FromResult<StorageFileData?>(new(
                ContentType: file.Metadata.ContentType,
                Size: file.Data.LongLength,
                Stream: stream,
                StreamLength: stream.Length));
        }

        return Task.FromResult<StorageFileData?>(null);
    }

#pragma warning disable CS1998 // This async method lacks 'await'
    public async IAsyncEnumerable<StorageFileMetadata> List(
        string path,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var f in storage.ListFiles(path))
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return f.Metadata;
        }
    }
#pragma warning restore CS1998
}
