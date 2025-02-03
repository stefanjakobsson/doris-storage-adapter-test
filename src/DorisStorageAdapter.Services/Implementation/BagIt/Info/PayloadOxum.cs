namespace DorisStorageAdapter.Services.Implementation.BagIt.Info;

internal sealed record PayloadOxum(
    long OctetCount,
    long StreamCount);