using System;
using System.ComponentModel.DataAnnotations;

namespace DorisStorageAdapter.Server.Configuration;

internal sealed record AuthorizationConfiguration
{
    public const string ConfigurationSection = "Authorization";

    [Required]
    public required string[] CorsAllowedOrigins { get; init; }

    [Required]
    public required Uri JwksUri { get; init; }
}
