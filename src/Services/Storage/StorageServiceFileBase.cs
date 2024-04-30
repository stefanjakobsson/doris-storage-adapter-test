using System;

namespace DatasetFileUpload.Services.Storage;

public record StorageServiceFileBase(
    string? ContentType,
    DateTime? DateCreated,
    DateTime? DateModified);
