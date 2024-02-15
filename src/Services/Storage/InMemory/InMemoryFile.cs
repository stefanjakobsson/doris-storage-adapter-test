using System;

namespace DatasetFileUpload.Services.Storage.InMemory;

internal record InMemoryFile(
    DateTime DateCreated,
    DateTime DateModified,
    byte[] Data);