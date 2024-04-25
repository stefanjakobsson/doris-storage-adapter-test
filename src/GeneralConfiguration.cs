using System.ComponentModel.DataAnnotations;

namespace DatasetFileUpload;

public record GeneralConfiguration
{
    [Required]
    [Url]
    public required string PublicUrl { get; init; }
}
