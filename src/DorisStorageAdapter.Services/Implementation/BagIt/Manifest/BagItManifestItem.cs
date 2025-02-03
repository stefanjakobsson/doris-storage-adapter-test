namespace DorisStorageAdapter.Services.Implementation.BagIt.Manifest;

internal sealed record BagItManifestItem(
    string FilePath,
    byte[] Checksum);