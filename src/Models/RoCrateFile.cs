using System;
using System.Text.Json.Serialization;

namespace DatasetFileUpload.Models;

public record RoCrateFile
{
    [JsonPropertyName("@type")]
    public string RdfType => "File";
    [JsonPropertyName("@id")]
    public required string Id { get; init; }
    public required FileTypeEnum AdditionalType { get; init; }
    public required long ContentSize { get; init; }
    public DateTime? DateCreated { get; init; }
    public DateTime? DateModified { get; init; }
    public string? EncodingFormat { get; init; }
    public string? Sha256 { get; init; }
    public Uri? Url { get; init; }
}