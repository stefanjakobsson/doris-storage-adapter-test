using System;

namespace DorisStorageAdapter.Services.Storage;

public record StorageServiceFileBase(
    string? ContentType,
    DateTime? DateCreated,
    DateTime? DateModified);
