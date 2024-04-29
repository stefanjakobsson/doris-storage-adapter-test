using System;

namespace DatasetFileUpload.Services.Storage;

public record StorageServiceFile : StorageServiceFileBase
{
    public required string Path { get; init; }
    public required long Size { get; init; }
}
