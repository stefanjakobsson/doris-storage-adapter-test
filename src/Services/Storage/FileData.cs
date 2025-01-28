namespace DorisStorageAdapter.Services.Storage;

public record FileData(
    string? ContentType,
    StreamWithLength Data,
    long Length);