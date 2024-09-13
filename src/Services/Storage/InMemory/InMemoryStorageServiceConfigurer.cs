using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DorisStorageAdapter.Services.Storage.InMemory;

internal sealed class InMemoryStorageServiceConfigurer : IStorageServiceConfigurer<InMemoryStorageService>
{
    public void Configure(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<InMemoryStorage>();
        services.AddTransient<IStorageService, InMemoryStorageService>();
    }
}
