using System;

namespace DorisStorageAdapter.Services.Implementation.Storage;

internal record StorageFileBaseMetadata(
    string? ContentType,
    DateTime? DateCreated,
    DateTime? DateModified);
