using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace DorisStorageAdapter.Services.Storage.InMemory;

internal class InMemoryStorage
{
    private readonly ConcurrentDictionary<string, InMemoryFile> files = [];

    public InMemoryFile AddOrUpdate(string filePath, byte[] data, string? contentType) =>
        files.AddOrUpdate(filePath,
            new InMemoryFile(new(
                ContentType: contentType,
                DateCreated: DateTime.UtcNow,
                DateModified: DateTime.UtcNow,
                Length: data.Length,
                Path: filePath), 
                data),
            (_, oldValue) => 
                new(oldValue.Metadata with
                {
                    ContentType = contentType,
                    DateModified = DateTime.UtcNow,
                    Length = data.LongLength
                }, 
                data));


    public void Remove(string filePath) => files.TryRemove(filePath, out var _);

    public bool TryGet(string filePath, [NotNullWhen(true)] out InMemoryFile? file) => 
        files.TryGetValue(filePath, out file);

    public IEnumerable<InMemoryFile> ListFiles(string path) =>
        files.Where(f => f.Key.StartsWith(path)).Select(f => f.Value);
}
