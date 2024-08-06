using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services.Storage.InMemory;

internal class InMemoryStorageService(InMemoryStorage storage) : IStorageService
{
    private readonly InMemoryStorage storage = storage;

    public async Task<StorageServiceFileBase> StoreFile(string filePath, FileData data)
    {
        using var memoryStream = new MemoryStream();
        await data.Stream.CopyToAsync(memoryStream);
        var byteArray = memoryStream.ToArray();

        return storage.AddOrUpdate(filePath, byteArray, data.ContentType).Metadata;
    }

    public Task DeleteFile(string filePath)
    {
        storage.Remove(filePath);
        return Task.CompletedTask;
    }

    public Task<FileData?> GetFileData(string filePath)
    {
        if (storage.TryGet(filePath, out var file))
        {
            return Task.FromResult<FileData?>(new(
                Stream: new MemoryStream(file.Data), 
                Length: file.Data.LongLength, 
                ContentType: file.Metadata.ContentType));
        }

        return Task.FromResult<FileData?>(null);
    }

    public async IAsyncEnumerable<StorageServiceFile> ListFiles(string path)
    {
        // This is a hack to avoid warning CS1998 (async method without await)
        await Task.CompletedTask;

        foreach (var f in storage.ListFiles(path))
        {
            yield return f.Metadata;
        }
    }
}
