namespace DorisStorageAdapter.Models.BagIt;

public record BagItManifestItem(
    string FilePath,
    byte[] Checksum);