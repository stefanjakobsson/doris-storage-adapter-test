using System;

namespace DorisStorageAdapter.Services.Storage;

public record BaseFileMetadata(
    string? ContentType,
    DateTime? DateCreated,
    DateTime? DateModified);
