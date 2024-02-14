using System;

namespace DatasetFileUpload.Services.Storage;

public class IllegalPathException(string path) : Exception
{
    public string Path { get; } = path;
}
