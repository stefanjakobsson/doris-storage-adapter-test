using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DatasetFileUpload.Services.Storage.InMemory;

internal class InMemoryStorageServiceConfigurer : IStorageServiceConfigurer<InMemoryStorageService>
{
    public void Configure(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<InMemoryStorage>();
        services.AddTransient<IStorageService, InMemoryStorageService>();
    }
}
