using System.ComponentModel.DataAnnotations;

namespace DatasetFileUpload.Services.Storage.S3;

internal record S3StorageServiceConfiguration
{
    [Required]
    public required string ServiceUrl { get; init; }
    [Required]
    public required string BucketName { get; init; }
    [Required]
    public required string AccessKey { get; init; }
    [Required]
    public required string SecretKey { get; init; }

    public bool ForcePathStyle { get; init; } = true;
    public long MultiPartUploadThreshold { get; init; } = 100 * 1024 * 1024;
    public long MultiPartUploadChunkSize { get; init; } = 10 * 1024 * 1024;
}
