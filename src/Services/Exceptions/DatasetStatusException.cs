namespace DorisStorageAdapter.Services.Exceptions;

internal sealed class DatasetStatusException : ApiException
{
    public DatasetStatusException() : base("Status mismatch.") 
    { 
    }

    public DatasetStatusException(string message) : base(message)
    {
    }

    public DatasetStatusException(string message, System.Exception innerException) : base(message, innerException)
    {
    }
}
