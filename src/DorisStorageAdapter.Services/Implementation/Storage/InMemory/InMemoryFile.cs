namespace DorisStorageAdapter.Services.Implementation.Storage.InMemory;

internal sealed record InMemoryFile(
    StorageFileMetadata Metadata,
    byte[] Data);