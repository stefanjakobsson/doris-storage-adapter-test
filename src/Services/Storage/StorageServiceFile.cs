using System;

namespace DatasetFileUpload.Services.Storage;

public record StorageServiceFile(
    string? ContentType,
    DateTime? DateCreated,
    DateTime? DateModified,
    string Path,
    long Size
) : StorageServiceFileBase(ContentType, DateCreated, DateModified);
