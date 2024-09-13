namespace DorisStorageAdapter.Services.BagIt;

internal sealed record BagItFetchItem(
    string FilePath,
    long? Length,
    string Url);
