namespace DatasetFileUpload.Models;

using System.Text.Json.Serialization;
using System.Text.Json;

public enum FileType{
    Data,
    Documentation,
    Metadata
}
public class RoCrateFile{
    [JsonPropertyName("@type")]
    public string RdfType { get; }
    public string Id { get; set; }
    public long? ContentSize { get; set; }
    public DateTime? DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
    public string? EncodingFormat { get; set; }
    public string? Sha256 { get; set; }
    public Uri? Url { get; set; }

    public RoCrateFile(){
        RdfType = "File";
    }
}