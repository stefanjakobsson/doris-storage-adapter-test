
using Microsoft.AspNetCore.Http;

namespace DatasetFileUpload.Models;

public record AddFileRequest(
    IFormFile File,
    string Folder);