using System.ComponentModel.DataAnnotations;

namespace DatasetFileUpload.Services.Storage.S3;

internal record S3StorageServiceConfiguration
{
    [Required]
    public required string BucketName { get; init; }
}
