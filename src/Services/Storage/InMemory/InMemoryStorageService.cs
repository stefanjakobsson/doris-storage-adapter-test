using DatasetFileUpload.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Storage.InMemory;

internal class InMemoryStorageService : IStorageService
{
    private readonly Dictionary<string, InMemoryFile> files = [];

    public async Task<RoCrateFile> StoreFile(string filePath, Stream stream)
    {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);

        var file = new InMemoryFile(DateTime.UtcNow, DateTime.UtcNow, memoryStream.ToArray());
        files[filePath] = file;

        return MapFile(filePath, file);
    }

    public Task DeleteFile(string filePath)
    {
        files.Remove(filePath);
        return Task.CompletedTask;
    }

    public Task<bool> FileExists(string filePath)
    {
        return Task.FromResult(files.ContainsKey(filePath));
    }

    public Task<StreamWithLength?> GetFileData(string filePath)
    {
        if (files.TryGetValue(filePath, out var file))
        {
            return Task.FromResult<StreamWithLength?>(new(new MemoryStream(file.Data), file.Data.LongLength));
        }

        return Task.FromResult<StreamWithLength?>(null);
    }

    public async IAsyncEnumerable<RoCrateFile> ListFiles(string path)
    {
        // This is a hack to avoid warning CS1998 (async method without await)
        await Task.CompletedTask;

        foreach (var value in files)
        {
            if (value.Key.StartsWith(path))
            {
                yield return MapFile(value.Key, value.Value);
            }
        }
    }

    private static RoCrateFile MapFile(string filePath, InMemoryFile file) => new()
    {
        Id = filePath,
        DateCreated = file.DateCreated,
        DateModified = file.DateModified,
        ContentSize = file.Data.LongLength
    };
}
