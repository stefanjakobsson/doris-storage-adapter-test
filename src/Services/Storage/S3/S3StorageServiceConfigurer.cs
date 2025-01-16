using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DorisStorageAdapter.Services.Storage.S3;

internal sealed class S3StorageServiceConfigurer : IStorageServiceConfigurer<S3StorageService>
{
    public void Configure(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptionsWithValidateOnStart<S3StorageServiceConfiguration>()
           .Bind(configuration)
           .ValidateDataAnnotations()
           .Validate(c =>
                c.MultiPartUploadThreshold < c.MultiPartUploadChunkSize * 10_000, 
                nameof(S3StorageServiceConfiguration.MultiPartUploadChunkSize) +
                " is too small to allow uploading larger objects than the value of " +
                nameof(S3StorageServiceConfiguration.MultiPartUploadThreshold) +
                " (max number of parts per upload is 10 000)");

        services.AddSingleton<IAmazonS3>(
            sp =>
            {
                var s3Config = sp.GetRequiredService<IOptions<S3StorageServiceConfiguration>>().Value;

                return new AmazonS3Client(s3Config.AccessKey, s3Config.SecretKey, new AmazonS3Config
                {
                    // Disable retries to avoid seeking in the input stream
                    // when uploading objects, see S3StorageService.StoreFile().
                    MaxErrorRetry = 0,
                    ServiceURL = s3Config.ServiceUrl,
                    ForcePathStyle = s3Config.ForcePathStyle
                });
            });

        services.AddTransient<IStorageService, S3StorageService>();
    }
}
