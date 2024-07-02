using Amazon.S3;
using Amazon.S3.Model;
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
        // Retries?

        if (data.Length <= configuration.MultiPartUploadThreshold)
        {
           await PutObject(filePath, data);
        }
        else
        {
           await MultiPartUpload(filePath, data);
        }

        return new(
            ContentType: null,
            DateCreated: null,
            // DateTime.UtcNow is an approximation.
            // To get the actual LastModified we need to call GetObjectMetadataAsync,
            // which is arguably unnecessary overhead.
            DateModified: DateTime.UtcNow);
    }

    private async Task PutObject(string filePath, FileData data)
    {
        var request = new PutObjectRequest()
        {
            BucketName = configuration.BucketName,
            Key = filePath,
            InputStream = data.Stream,
            AutoCloseStream = false,
            AutoResetStreamPosition = false,
        };

        request.Headers.ContentLength = data.Length;

        await client.PutObjectAsync(request);
    }

    private async Task MultiPartUpload(string filePath, FileData data)
    {
        // Abort multi part upload on error?
        // Retry?

        var response = await client.InitiateMultipartUploadAsync(new()
        {
            BucketName = configuration.BucketName,
            Key = filePath
        });

        int chunkSize = configuration.MultiPartUploadChunkSize;
        int chunk = 1;
        long bytesRemaining = data.Length;
        var parts = new List<PartETag>();
        var stream = new MultiPartUploadStream(data.Stream);

        while (bytesRemaining > 0)
        {
            int partSize = bytesRemaining > chunkSize ? chunkSize : (int)bytesRemaining;
            stream.Reset(partSize);

            var uploadPartResponse = await client.UploadPartAsync(new()
            {
                BucketName = configuration.BucketName,
                Key = filePath,
                UploadId = response.UploadId,
                PartSize = partSize,
                PartNumber = chunk,
                InputStream = stream,
            });

            parts.Add(new(uploadPartResponse));

            bytesRemaining -= partSize;
            chunk++;
        }

        await client.CompleteMultipartUploadAsync(new()
        {
            BucketName = configuration.BucketName,
            Key = filePath,
            UploadId = response.UploadId,
            PartETags = parts
        });
    }

    public Task DeleteFile(string filePath)
    {
        return client.DeleteObjectAsync(new()
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

            // Do we need to check response HTTP status code?

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
        var request = new ListObjectsV2Request
        {
            BucketName = configuration.BucketName,
            Prefix = path

        };

        ListObjectsV2Response response;

        do
        {
            response = await client.ListObjectsV2Async(request);

            foreach (var file in response.S3Objects)
            {
                yield return new(
                    ContentType: null,
                    DateCreated: null,
                    DateModified: file.LastModified.ToUniversalTime(),
                    Path: file.Key,
                    Length: file.Size);
            }

            request.ContinuationToken = response.NextContinuationToken;
        }
        while (response.IsTruncated);
    }
}
