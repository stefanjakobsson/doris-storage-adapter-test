using System.ComponentModel.DataAnnotations;

namespace DatasetFileUpload.Services.Storage.Disk;

internal record FileSystemStorageServiceConfiguration
{
    public const string ConfigurationSection = "Storage:FileSystemStorageService";

    [Required]
    public required string BasePath { get; init; }
}
