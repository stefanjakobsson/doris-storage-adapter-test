namespace DorisStorageAdapter.Services.Exceptions;

public class DatasetStatusException : ApiException
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
