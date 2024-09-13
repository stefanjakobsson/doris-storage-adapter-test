using System.ComponentModel.DataAnnotations;
using System.IO;

namespace DorisStorageAdapter.Services.Storage.FileSystem;

internal sealed record FileSystemStorageServiceConfiguration
{
    [Required]
    public required string BasePath { get; init; }

    public string TempFilePath { get; init; } = Path.GetTempPath();
}
