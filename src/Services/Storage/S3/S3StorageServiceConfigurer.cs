using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DatasetFileUpload.Services.Storage.S3;

internal class S3StorageServiceConfigurer : IStorageServiceConfigurer<S3StorageService>
{
    public void Configure(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptionsWithValidateOnStart<S3StorageServiceConfiguration>()
           .Bind(configuration)
           .ValidateDataAnnotations();

        var s3Config = configuration.Get<S3StorageServiceConfiguration>()!;

        services.AddSingleton<IAmazonS3>(
            new AmazonS3Client(s3Config.AccessKey, s3Config.SecretKey, new AmazonS3Config
            {
                ServiceURL = s3Config.ServiceUrl,
                ForcePathStyle = s3Config.ForcePathStyle
            }));

        services.AddTransient<IStorageService, S3StorageService>();
    }
}
