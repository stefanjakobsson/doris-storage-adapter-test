using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DatasetFileUpload.Authorization;

internal record AuthorizationConfiguration
{
    public const string ConfigurationSection = "Authorization";

    [Required]
    public required string[] CorsAllowedOrigins { get; init; }

    [Required]
    [Url]
    public required string JwksUri { get; init; }
}
