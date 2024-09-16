using System;

namespace DorisStorageAdapter.Services.Exceptions;

public abstract class ApiException : Exception
{
    public int StatusCode { get; } = 400;

    protected ApiException() : base()
    {
    }

    protected ApiException(int statusCode) : base()
    {
        StatusCode = statusCode;
    }

    protected ApiException(string message) : base(message)
    {
    }

    protected ApiException(string message, int statusCode) : this(message)
    {
        StatusCode = statusCode;
    }

    protected ApiException(string message, Exception innerException) : base(message, innerException)
    {
    }

    protected ApiException(string message, Exception innerException, int statusCode) : this(message, innerException)
    {
        StatusCode = statusCode;
    }
}
