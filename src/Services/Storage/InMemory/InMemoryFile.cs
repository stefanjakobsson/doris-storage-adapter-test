namespace DorisStorageAdapter.Services.Storage.InMemory;

internal sealed record InMemoryFile(
    StorageServiceFile Metadata,
    byte[] Data);