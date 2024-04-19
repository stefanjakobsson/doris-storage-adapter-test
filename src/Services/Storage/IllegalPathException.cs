namespace DatasetFileUpload.Services.Storage;

internal class IllegalPathException(string path) : ApiException("Illegal path.")
{
    public string Path { get; } = path;
}
