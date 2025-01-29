using System;

namespace DorisStorageAdapter.Services.Storage;

internal sealed record FileMetadata(
    string? ContentType,
    DateTime? DateCreated,
    DateTime? DateModified,
    string Path,
    long Length
) : BaseFileMetadata(ContentType, DateCreated, DateModified);
