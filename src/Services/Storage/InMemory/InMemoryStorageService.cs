using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Storage.InMemory;

internal class InMemoryStorageService : IStorageService
{
    private readonly Dictionary<string, InMemoryFile> files = [];

    public async Task<StorageServiceFileBase> StoreFile(string filePath, FileData data)
    {
        using var memoryStream = new MemoryStream();
        await data.Stream.CopyToAsync(memoryStream);
        var byteArray = memoryStream.ToArray();

        if (files.TryGetValue(filePath, out InMemoryFile? file))
        {
            file.Data = byteArray;
            file.Metadata = file.Metadata with
            {
                ContentType = data.ContentType,
                DateModified = DateTime.UtcNow,
                Length = byteArray.LongLength
            };
        }
        else
        {
            file = new(new(
                ContentType: data.ContentType,
                DateCreated: DateTime.UtcNow,
                DateModified: DateTime.UtcNow,
                Length: data.Length,
                Path: filePath),
            byteArray);

            files[filePath] = file;
        }

        return file.Metadata;
    }

    public Task DeleteFile(string filePath)
    {
        files.Remove(filePath);
        return Task.CompletedTask;
    }

    public Task<FileData?> GetFileData(string filePath)
    {
        if (files.TryGetValue(filePath, out var file))
        {
            return Task.FromResult<FileData?>(new(
                new MemoryStream(file.Data), file.Data.LongLength, file.Metadata.ContentType));
        }

        return Task.FromResult<FileData?>(null);
    }

    public async IAsyncEnumerable<StorageServiceFile> ListFiles(string path)
    {
        // This is a hack to avoid warning CS1998 (async method without await)
        await Task.CompletedTask;

        foreach (var value in files)
        {
            if (value.Key.StartsWith(path))
            {
                yield return value.Value.Metadata;
            }
        }
    }
}
