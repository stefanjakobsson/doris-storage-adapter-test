using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Storage.InMemory;

internal class InMemoryStorageService : IStorageService
{
    private readonly Dictionary<string, InMemoryFile> files = [];

    public async Task<StorageServiceFileBase> StoreFile(string filePath, StreamWithLength data, string? contentType)
    {
        using var memoryStream = new MemoryStream();
        await data.Stream.CopyToAsync(memoryStream);
        var byteArray = memoryStream.ToArray();

        if (files.TryGetValue(filePath, out InMemoryFile? file))
        {
            file.Data = byteArray;
            file.Metadata = file.Metadata with
            {
                DateModified = DateTime.UtcNow,
                Size = byteArray.LongLength,
                ContentType = contentType
            };
        }
        else
        {
            file = new(new(
                ContentType: contentType,
                DateCreated: DateTime.UtcNow,
                DateModified: DateTime.UtcNow,
                Path: filePath,
                Size: data.Length),
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

    public Task<StreamWithLength?> GetFileData(string filePath)
    {
        if (files.TryGetValue(filePath, out var file))
        {
            return Task.FromResult<StreamWithLength?>(new(new MemoryStream(file.Data), file.Data.LongLength));
        }

        return Task.FromResult<StreamWithLength?>(null);
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
