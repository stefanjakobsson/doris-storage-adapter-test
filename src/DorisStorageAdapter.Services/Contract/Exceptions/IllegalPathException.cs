namespace DorisStorageAdapter.Services.Contract.Exceptions;

public sealed class IllegalPathException : ServiceException
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
