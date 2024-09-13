namespace DorisStorageAdapter.Services.BagIt;

internal sealed record BagItManifestItem(
    string FilePath,
    byte[] Checksum);