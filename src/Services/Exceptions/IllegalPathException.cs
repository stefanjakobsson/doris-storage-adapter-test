namespace DatasetFileUpload.Services.Exceptions;

internal class IllegalPathException(string path) : ApiException("Illegal path.")
{
    public string Path { get; } = path;
}
