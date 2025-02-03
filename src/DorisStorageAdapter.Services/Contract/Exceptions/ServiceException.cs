using System;

namespace DorisStorageAdapter.Services.Contract.Exceptions;

public abstract class ServiceException : Exception
{
    protected ServiceException() : base()
    {
    }

    protected ServiceException(string message) : base(message)
    {
    }

    protected ServiceException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
