using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DorisStorageAdapter.Services.Implementation.Storage.FileSystem;

internal sealed class FileSystemStorageServiceConfigurer : IStorageServiceConfigurer<FileSystemStorageService>
{
    public void Configure(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptionsWithValidateOnStart<FileSystemStorageServiceConfiguration>()
           .Bind(configuration)
           .ValidateDataAnnotations();

        services.AddTransient<IStorageService, FileSystemStorageService>();
    }
}
