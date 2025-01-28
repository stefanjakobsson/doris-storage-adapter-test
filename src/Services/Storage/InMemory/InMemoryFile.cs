namespace DorisStorageAdapter.Services.Storage.InMemory;

internal sealed record InMemoryFile(
    FileMetadata Metadata,
    byte[] Data);