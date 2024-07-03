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
        var response = await client.InitiateMultipartUploadAsync(new()
        {
            BucketName = configuration.BucketName,
            Key = filePath
        });

        try
        {
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
        catch
        {
            try
            {
                await client.AbortMultipartUploadAsync(new()
                {
                    BucketName = configuration.BucketName,
                    Key = filePath,
                    UploadId = response.UploadId
                });
            }
            catch { }

            throw;
        }
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
