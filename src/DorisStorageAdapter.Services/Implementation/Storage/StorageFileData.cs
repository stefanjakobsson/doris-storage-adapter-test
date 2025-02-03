using System.IO;

namespace DorisStorageAdapter.Services.Implementation.Storage;

internal sealed record StorageFileData(
    string? ContentType,
    long Size,
    Stream Stream,
    long StreamLength);
