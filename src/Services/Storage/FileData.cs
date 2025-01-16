using System.IO;

namespace DorisStorageAdapter.Services.Storage;

public record FileData(
    Stream Stream,
    long StreamLength,
    string? ContentType);