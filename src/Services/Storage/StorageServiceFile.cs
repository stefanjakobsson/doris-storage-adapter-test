using System;

namespace DatasetFileUpload.Services.Storage;

public record StorageServiceFile
{
    public required string Path { get; init; }
    public required long Size { get; init; }
    public DateTime? DateCreated { get; init; }
    public DateTime? DateModified { get; init; }
    public string? ContentType { get; init; }
}
