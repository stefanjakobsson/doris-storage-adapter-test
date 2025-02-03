namespace DorisStorageAdapter.Services.Implementation.BagIt.Info;

internal sealed record BagCount(
    long Ordinal,
    long? TotalCount);
