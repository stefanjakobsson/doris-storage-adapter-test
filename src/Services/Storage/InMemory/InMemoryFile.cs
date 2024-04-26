namespace DatasetFileUpload.Services.Storage.InMemory;

internal class InMemoryFile(
    StorageServiceFile metadata,
    byte[] data)
{
    public StorageServiceFile Metadata { get; set; } = metadata;
    public byte[] Data { get; set; } = data;
}