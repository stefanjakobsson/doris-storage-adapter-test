using DorisStorageAdapter.Configuration;
using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace DorisStorageAdapter.Services.Storage.NextCloud;

internal sealed record NextCloudStorageServiceConfiguration
{
    private readonly Uri baseUrl;

    [Required]
    public required Uri BaseUrl 
    {
        get => baseUrl;


        [MemberNotNull(nameof(baseUrl))]
        init
        {
            baseUrl = UriHelpers.EnsureUriEndsWithSlash(value);
        }
    }

    [Required]
    public required string BasePath { get; init; }
    [Required]
    public required string TempFilePath { get; init; }
    [Required]
    public required string User { get; init; }
    [Required]
    public required string Password { get; init; }

    public long ChunkedUploadThreshold { get; init; } = 100 * 1024 * 1024;
    public long ChunkedUploadChunkSize { get; init; } = 10 * 1024 * 1024;
}
