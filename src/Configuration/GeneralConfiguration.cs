using System;
using System.ComponentModel.DataAnnotations;

namespace DorisStorageAdapter.Configuration;

public record GeneralConfiguration
{
    [Required]
    public required Uri PublicUrl { get; init; }
}
