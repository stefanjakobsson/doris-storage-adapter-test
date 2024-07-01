using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
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


        var now = DateTime.UtcNow;

        if (data.Length > configuration.MultiPartUploadThreshold)
        {
            // Abort multi part upload on error?
            // Retry?

            var response = await client.InitiateMultipartUploadAsync(new()
            {
                BucketName = configuration.BucketName,
                Key = filePath
            });

            int chunkSize = configuration.MultiPartUploadPartSize;
            byte[] buffer = new byte[configuration.MultiPartUploadPartSize];
            using var memoryStream = new MemoryStream(buffer);
            int chunk = 1;
            long bytesWritten = 0;
            var parts = new List<PartETag>();

            while (bytesWritten < data.Length)
            {
                long bytesLeft = data.Length - bytesWritten;
                int partSize = bytesLeft >= chunkSize ? chunkSize : (int)bytesLeft;

                await data.Stream.ReadExactlyAsync(buffer, 0, partSize);
                memoryStream.Position = 0;

                var uploadPartResponse = await client.UploadPartAsync(new()
                {
                    BucketName = configuration.BucketName,
                    Key = filePath,
                    UploadId = response.UploadId,
                    PartSize = partSize,
                    PartNumber = chunk,
                    InputStream = memoryStream
                });

                parts.Add(new(uploadPartResponse));

                bytesWritten += partSize;
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
        else
        {
            // Check http return code?

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

        return new(
            ContentType: null,
            DateCreated: null,
            // This is an approximation, to get the real
            // value we need to call GetObjectMetadataAsync
            DateModified: now);
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

            // If the response is truncated, set the request ContinuationToken
            // from the NextContinuationToken property of the response.
            request.ContinuationToken = response.NextContinuationToken;
        }
        while (response.IsTruncated);
    }
}
