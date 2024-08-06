using System.ComponentModel.DataAnnotations;

namespace DorisStorageAdapter;

public record GeneralConfiguration
{
    [Required]
    [Url]
    public required string PublicUrl { get; init; }
}
