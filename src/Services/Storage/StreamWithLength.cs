using System.IO;

namespace DorisStorageAdapter.Services.Storage;

public record StreamWithLength(
    Stream Stream,
    long Length);
