using System;

namespace DorisStorageAdapter.Services.Storage;

public record FileMetadata(
    string? ContentType,
    DateTime? DateCreated,
    DateTime? DateModified,
    string Path,
    long Length
) : BaseFileMetadata(ContentType, DateCreated, DateModified);
