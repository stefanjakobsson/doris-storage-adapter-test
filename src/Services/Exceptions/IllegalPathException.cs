namespace DorisStorageAdapter.Services.Exceptions;

internal sealed class IllegalPathException(string path) : ApiException("Illegal path.")
{
    public string Path { get; } = path;
}
