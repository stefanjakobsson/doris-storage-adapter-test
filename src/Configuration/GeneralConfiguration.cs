using System.ComponentModel.DataAnnotations;

namespace DatasetFileUpload.Configuration;

public record GeneralConfiguration
{
    [Required]
    [Url]
    public required string JwksUri { get; init; }

    [Required]
    [Url]
    public required string PublicUrl { get; init; }
}
