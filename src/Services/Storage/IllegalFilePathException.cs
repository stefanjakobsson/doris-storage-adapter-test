using System;

namespace DatasetFileUpload.Services.Storage;

public class IllegalFilePathException(string filePath) : Exception
{
    public string FilePath { get; } = filePath;
}
