using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Storage.S3;

internal class S3StorageService(IAmazonS3 client) : IStorageService
{
    private readonly IAmazonS3 client = client;

    public async Task<StorageServiceFileBase> StoreFile(string filePath, FileData data)
    {
        var now = DateTime.UtcNow;

        var request = new PutObjectRequest()
        {
            BucketName = "test",
            Key = filePath,
            InputStream = data.Stream,
            AutoCloseStream = false,
            AutoResetStreamPosition = false,           
        };

        request.Headers.ContentLength = data.Length;
        //request.Metadata.Add("date-created", now.ToString());

        await client.PutObjectAsync(request);


        return new(
            ContentType: null, //?
            DateCreated: now,
            DateModified: now);
    }

    public Task DeleteFile(string filePath)
    {
        return client.DeleteObjectAsync(new()
        {
            BucketName = "test",
            Key = filePath
        });
    }

    public async Task<FileData?> GetFileData(string filePath)
    {
        try
        {
            var response = await client.GetObjectAsync(new()
            {
                BucketName = "test",
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
            BucketName = "test",
            Prefix = path
        };

        ListObjectsV2Response response;

        do
        {
            response = await client.ListObjectsV2Async(request);

            foreach (var file in response.S3Objects)
            {
                yield return new(
                    ContentType: null, // ? Kan inte få från ListObjects
                    DateCreated: null, // Hämta från metadata, går det?
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
