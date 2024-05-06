using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DatasetFileUpload.Services.Storage.FileSystem;

internal class FileSystemStorageServiceConfigurer : IStorageServiceConfigurer<FileSystemStorageService>
{
    public void Configure(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptionsWithValidateOnStart<FileSystemStorageServiceConfiguration>()
           .Bind(configuration)
           .ValidateDataAnnotations();

        services.AddTransient<IStorageService, FileSystemStorageService>();
    }
}
