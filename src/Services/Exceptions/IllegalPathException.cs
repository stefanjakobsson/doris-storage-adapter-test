namespace DorisStorageAdapter.Services.Exceptions;

public class IllegalPathException : ApiException
{
    public IllegalPathException() : base("Illegal path.")
    {
    }

    public IllegalPathException(string message, System.Exception innerException) : base(message, innerException)
    {
    }

    public IllegalPathException(string message) : base(message)
    {
    }
}
