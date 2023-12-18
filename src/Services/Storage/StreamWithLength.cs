using System.IO;

namespace DatasetFileUpload.Services.Storage;

public record StreamWithLength(
    Stream Stream,
    long Length);
