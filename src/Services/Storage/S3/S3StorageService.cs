using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Storage.S3;

internal class S3StorageService(
    IAmazonS3 client,
    IOptions<S3StorageServiceConfiguration> configuration) : IStorageService
{
    private readonly IAmazonS3 client = client;
    private readonly S3StorageServiceConfiguration configuration = configuration.Value;

    public async Task<StorageServiceFileBase> StoreFile(string filePath, FileData data)
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

        await utility.UploadAsync(request);
        
        return new(
            ContentType: null,
            DateCreated: null,
            // DateTime.UtcNow is an approximation.
            // To get the actual LastModified we need to call GetObjectMetadataAsync,
            // which is arguably unnecessary overhead.
            DateModified: DateTime.UtcNow);
    }

    public async Task DeleteFile(string filePath)
    {
        await client.DeleteObjectAsync(new()
        {
            BucketName = configuration.BucketName,
            Key = filePath
        });
    }

    public async Task<FileData?> GetFileData(string filePath)
    {
        try
        {
            var response = await client.GetObjectAsync(new()
            {
                BucketName = configuration.BucketName,
                Key = filePath
            });

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

    public async IAsyncEnumerable<StorageServiceFile> ListFiles(string path)
    {
        var paginator = client.Paginators.ListObjectsV2(new()
        {
            BucketName = configuration.BucketName,
            Prefix = path
        });

        await foreach (var file in paginator.S3Objects)
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
