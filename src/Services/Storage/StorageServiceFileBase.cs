using System;

namespace DatasetFileUpload.Services.Storage;

public record StorageServiceFileBase
{
    public DateTime? DateCreated { get; init; }
    public DateTime? DateModified { get; init; }
    public string? ContentType { get; init; }
}
