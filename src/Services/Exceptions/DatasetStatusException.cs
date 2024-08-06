namespace DorisStorageAdapter.Services.Exceptions;

internal class DatasetStatusException : ApiException
{
    public DatasetStatusException() : base("Status mismatch.") { }
}
