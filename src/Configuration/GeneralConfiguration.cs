using System.ComponentModel.DataAnnotations;

namespace DorisStorageAdapter.Configuration;

public record GeneralConfiguration
{
    [Required]
    [Url]
    public required string PublicUrl { get; init; }
}
