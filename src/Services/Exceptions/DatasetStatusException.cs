namespace DorisStorageAdapter.Services.Exceptions;

internal sealed class DatasetStatusException : ApiException
{
    public DatasetStatusException() : base("Status mismatch.") { }
}
