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

        services.AddSingleton<IAmazonS3>(p =>
        {
            var config = new AmazonS3Config
            {
                ServiceURL = "http://localhost:9000",
                ForcePathStyle = true
            };
            return new AmazonS3Client("test", "testtest", config);
        });

        services.AddTransient<IStorageService, S3StorageService>();
    }
}
