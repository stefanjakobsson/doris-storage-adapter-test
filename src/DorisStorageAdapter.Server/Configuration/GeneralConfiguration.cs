using DorisStorageAdapter.Helpers;
using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace DorisStorageAdapter.Server.Configuration;

public sealed record GeneralConfiguration
{
    private readonly Uri publicUrl;

    [Required]
    public required Uri PublicUrl
    {
        get => publicUrl;

        [MemberNotNull(nameof(publicUrl))]
        init
        {
            publicUrl = UriHelpers.EnsureUriEndsWithSlash(value);
        }
    }
}
