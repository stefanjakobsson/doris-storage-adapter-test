using System.ComponentModel.DataAnnotations;

namespace DorisStorageAdapter.Configuration;

internal sealed record AuthorizationConfiguration
{
    public const string ConfigurationSection = "Authorization";

    [Required]
    public required string[] CorsAllowedOrigins { get; init; }

    [Required]
    [Url]
    public required string JwksUri { get; init; }
}
