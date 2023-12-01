using System;

namespace DatasetFileUpload.Services.Storage;

public class IllegalFileNameException : Exception
{
    public IllegalFileNameException(string fileName) => FileName = fileName;

    public string FileName { get; }
}
