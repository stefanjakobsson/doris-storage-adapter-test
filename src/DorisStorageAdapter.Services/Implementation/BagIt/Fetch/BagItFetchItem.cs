namespace DorisStorageAdapter.Services.Implementation.BagIt.Fetch;

internal sealed record BagItFetchItem(
    string FilePath,
    long? Length,
    string Url);
