namespace DatasetFileUpload.Models.BagIt;

public class BagItManifestItem
{
    public required string FilePath { get; set; }
    public required byte[] Checksum { get; set; }
}
