namespace DorisStorageAdapter.Services.Storage.InMemory;

internal record InMemoryFile(
    StorageServiceFile Metadata,
    byte[] Data);