using System.ComponentModel.DataAnnotations;
using System.IO;

namespace DatasetFileUpload.Services.Storage.Disk;

internal record FileSystemStorageServiceConfiguration
{
    public const string ConfigurationSection = "Storage:FileSystemStorageService";

    [Required]
    public required string BasePath { get; init; }

    public string TempFilePath { get; init; } = Path.GetTempPath();
}
