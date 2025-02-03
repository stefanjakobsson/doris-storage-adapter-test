using System;

namespace DorisStorageAdapter.Services.Implementation.Storage.FileSystem;

public class IllegalFileSystemPathException : Exception
{
    public IllegalFileSystemPathException()
    {
    }

    public IllegalFileSystemPathException(string message, System.Exception innerException) : base(message, innerException)
    {
    }

    public IllegalFileSystemPathException(string message) : base(message)
    {
    }
}
