using System;

namespace DorisStorageAdapter.Services.Implementation.Storage;

internal sealed record StorageFileMetadata(
    string? ContentType,
    DateTime? DateCreated,
    DateTime? DateModified,
    string Path,
    long Size
) : StorageFileBaseMetadata(ContentType, DateCreated, DateModified);
