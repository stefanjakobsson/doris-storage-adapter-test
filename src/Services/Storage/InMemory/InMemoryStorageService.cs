using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services.Storage.InMemory;

internal sealed class InMemoryStorageService(InMemoryStorage storage) : IStorageService
{
    private readonly InMemoryStorage storage = storage;

    public async Task<StorageServiceFileBase> StoreFile(
        string filePath, 
        FileData data,
        CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        await data.Stream.CopyToAsync(memoryStream, cancellationToken);
        var byteArray = memoryStream.ToArray();

        return storage.AddOrUpdate(filePath, byteArray, data.ContentType).Metadata;
    }

    public Task DeleteFile(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        storage.Remove(filePath);
        return Task.CompletedTask;
    }

    public Task<PartialFileData?> GetFileData(string filePath, ByteRange? byteRange, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (storage.TryGet(filePath, out var file))
        {
            Stream stream = new MemoryStream(file.Data);

            if (byteRange != null)
            {
                stream = StreamHelpers.CreateByteRangeStream(stream, byteRange);
            }

            return Task.FromResult<PartialFileData?>(new(
                Stream: stream, 
                StreamLength: stream.Length,
                TotalLength: file.Data.LongLength,
                ContentType: file.Metadata.ContentType));
        }

        return Task.FromResult<PartialFileData?>(null);
    }

#pragma warning disable CS1998 // This async method lacks 'await'
    public async IAsyncEnumerable<StorageServiceFile> ListFiles(
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
