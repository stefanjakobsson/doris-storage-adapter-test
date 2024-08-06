namespace DorisStorageAdapter.Models.BagIt;

public record BagItFetchItem(
    string FilePath,
    long? Length,
    string Url);
