
using Microsoft.AspNetCore.Http;

namespace DatasetFileUpload.Models;

public enum FileType{
    Data,
    Documentation,
    Metadata
}

public record AddFileRequest(
    IFormFile File,
    string Folder);