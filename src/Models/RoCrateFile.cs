namespace DatasetFileUpload.Models;

using System;
using System.Text.Json.Serialization;

public enum UploadType
{
    Data,
    Documentation,
    Metadata
}

public class RoCrateFile
{
    [JsonPropertyName("@type")]
    public string RdfType => "File";
    public string Id { get; set; } = "";
    public long? ContentSize { get; set; }
    public DateTime? DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
    public string? EncodingFormat { get; set; }
    public string? Sha256 { get; set; }
    public Uri? Url { get; set; }
}