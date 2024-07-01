using System.ComponentModel.DataAnnotations;

namespace DatasetFileUpload.Services.Storage.S3;

internal record S3StorageServiceConfiguration
{
    [Required]
    public required string BucketName { get; init; }
    public long MultiPartUploadThreshold { get; init; } = 100 * 1024 * 1024;
    public int MultiPartUploadPartSize { get; init; } = 5 * 1024 * 1024;
}
