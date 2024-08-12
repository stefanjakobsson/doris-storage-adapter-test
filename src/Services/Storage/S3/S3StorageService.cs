using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services.Storage.S3;

internal class S3StorageService(
    IAmazonS3 client,
    IOptions<S3StorageServiceConfiguration> configuration) : IStorageService
{
    private readonly IAmazonS3 client = client;
    private readonly S3StorageServiceConfiguration configuration = configuration.Value;

    public async Task<StorageServiceFileBase> StoreFile(
        string filePath, 
        FileData data,
        CancellationToken cancellationToken)
    {
        var utility = new TransferUtility(client, new()
        {
            MinSizeBeforePartUpload = configuration.MultiPartUploadThreshold
        });

        var request = new TransferUtilityUploadRequest
        {
            AutoCloseStream = false,
            AutoResetStreamPosition = false,
            BucketName = configuration.BucketName,
            Key = filePath,
            InputStream = new StreamWrapper(data.Stream, data.Length),
            PartSize = configuration.MultiPartUploadChunkSize
        };

        await utility.UploadAsync(request, cancellationToken);
        
        return new(
            ContentType: null,
            DateCreated: null,
            // DateTime.UtcNow is an approximation.
            // To get the actual LastModified we need to call GetObjectMetadataAsync,
            // which is arguably unnecessary overhead.
            DateModified: DateTime.UtcNow);
    }

    public async Task DeleteFile(string filePath, CancellationToken cancellationToken)
    {
        await client.DeleteObjectAsync(new()
        {
            BucketName = configuration.BucketName,
            Key = filePath
        },
        cancellationToken);
    }

    public async Task<FileData?> GetFileData(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetObjectAsync(new()
            {
                BucketName = configuration.BucketName,
                Key = filePath
            }, 
            cancellationToken);

            return new(
                Stream: response.ResponseStream,
                Length: response.ContentLength,
                ContentType: null);
        }
        catch (AmazonS3Exception e)
        {
            if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            throw;
        }
    }

    public async IAsyncEnumerable<StorageServiceFile> ListFiles(
        string path,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var paginator = client.Paginators.ListObjectsV2(new()
        {
            BucketName = configuration.BucketName,
            Prefix = path
        });

        await foreach (var file in paginator.S3Objects.WithCancellation(cancellationToken))
        {
            yield return new(
                ContentType: null,
                DateCreated: null,
                DateModified: file.LastModified.ToUniversalTime(),
                Path: file.Key,
                Length: file.Size);
        }
    }
}
