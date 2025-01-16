using System.IO;

namespace DorisStorageAdapter.Services.Storage;

public record PartialFileData(
    Stream Stream,
    long StreamLength,
    long TotalLength,
    string? ContentType
) : FileData(Stream, StreamLength, ContentType);
