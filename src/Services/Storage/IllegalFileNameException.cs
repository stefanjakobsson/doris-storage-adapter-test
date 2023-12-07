using System;

namespace DatasetFileUpload.Services.Storage;

public class IllegalFileNameException(string fileName) : Exception
{
    public string FileName { get; } = fileName;
}
