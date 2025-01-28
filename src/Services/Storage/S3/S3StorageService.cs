using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services.Storage.S3;

internal sealed class S3StorageService(
    IAmazonS3 client,
    IOptions<S3StorageServiceConfiguration> configuration) : IStorageService
{
    private readonly IAmazonS3 client = client;
    private readonly S3StorageServiceConfiguration configuration = configuration.Value;

    public async Task<BaseFileMetadata> StoreFile(
        string filePath, 
        StreamWithLength data,
        string? contentType,
        CancellationToken cancellationToken)
    {
        using var utility = new TransferUtility(client, new()
        {
            MinSizeBeforePartUpload = configuration.MultiPartUploadThreshold
        });

        var request = new TransferUtilityUploadRequest
        {
            AutoCloseStream = false,
            AutoResetStreamPosition = false,
            BucketName = configuration.BucketName,
            Key = filePath,

            InputStream = data.Length == 0
                // Using Stream.Null when data.Length is 0 is a workaround to make sure
                // that TransferUtility does not read synchronously from data.Stream, which
                // (for some reason) happens if the stream is empty. Trying to read synchrounously
                // triggers an ASP.NET core error unless AllowSynchronousIO is set to true.
                ? System.IO.Stream.Null

                /// In order for TransferUtility to support multipart uploading
                /// without buffering each part in memory, InputStream must report Length
                /// and be seekable. Buffering should be avoided since it means that
                /// the value of configuration.MultiPartUploadChunkSize directly affects
                /// memory usage.
                /// 
                /// To make data.Stream seem seekable it is wrapped in a FakeSeekableStream. 
                /// Seeking is only actually used by TransferUtility when retrying a failed upload,
                /// so retries are disabled in S3StorageServiceConfigurer to avoid seeking here.
                : new FakeSeekableStream(data.Stream, data.Length),
           
            PartSize = configuration.MultiPartUploadChunkSize
        };

        await utility.UploadAsync(request, cancellationToken);
        
        return new(
            ContentType: null,
            DateCreated: null,
            // DateTime.UtcNow is an approximation.
            // Must call GetObjectMetadataAsync to get the actual LastModified value,
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

    public async Task<FileMetadata?> GetFileMetadata(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetObjectMetadataAsync(new()
            {
                BucketName = configuration.BucketName,
                Key = filePath
            },
            cancellationToken);

            return new(
                ContentType: null,
                DateCreated: null,
                DateModified: response.LastModified.ToUniversalTime(),
                Path: filePath,
                Length: response.ContentLength);
        }
        catch (AmazonS3Exception e)
        {
            if (e.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            throw;
        }
    }

    public async Task<FileData?> GetFileData(string filePath, ByteRange? byteRange, CancellationToken cancellationToken)
    {
        try
        {
            var request = new GetObjectRequest()
            {
                BucketName = configuration.BucketName,
                Key = filePath
            };

            if (byteRange != null)
            {
                request.ByteRange = new(byteRange.ToHttpRangeValue());
            }

            var response = await client.GetObjectAsync(request, cancellationToken);

            return new(
                Data: new(
                    Stream: response.ResponseStream, 
                    Length: response.ContentLength),           
                Length:
                    response.HttpStatusCode == HttpStatusCode.PartialContent
                        ? ContentRangeHeaderValue.TryParse(response.ContentRange, out var contentRange) 
                            ? contentRange.Length.GetValueOrDefault() 
                            : 0
                        : response.ContentLength,
                ContentType: null);
        }
        catch (AmazonS3Exception e)
        {
            if (e.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (e.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                // For some (stupid) reason, the respone HTTP headers can not be accessed here,
                // which means that the Content-Range header can not be used to get the
                // TotalLength value. Resort to issuing a new request to S3 to get the length.

                GetObjectMetadataResponse response;
                try
                {
                    response = await client.GetObjectMetadataAsync(new()
                    {
                        BucketName = configuration.BucketName,
                        Key = filePath
                    },
                    cancellationToken);
                }
                catch (AmazonS3Exception e2)
                {
                    if (e2.StatusCode == HttpStatusCode.NotFound)
                    {
                        return null;
                    }

                    throw;
                }

                // Return an empty stream to indicate that the
                // requested range was not satisfiable.
                return new(
                    Data: new(Stream: System.IO.Stream.Null, Length: 0),
                    Length: response.ContentLength,
                    ContentType: null); 
            }

            throw;
        }
    }

    public async IAsyncEnumerable<FileMetadata> ListFiles(
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
