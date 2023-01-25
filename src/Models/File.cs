namespace DatasetFileUpload.Models;

public enum FileType{
    Data,
    Documentation,
    Metadata
}

public record RoCrateFile(
    string Id,
    long? ContentSize = null,
    DateTime? DateCreated = null,
    DateTime? DateModified = null,
    string? EncodingFormat = null,
    string? Sha256 = null,
    Uri? Url = null);