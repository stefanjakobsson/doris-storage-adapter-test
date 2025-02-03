using System;

namespace DorisStorageAdapter.Services.Implementation.Configuration;

public sealed class StorageConfigurationException : Exception
{
    public StorageConfigurationException() : base()
    {
    }

    public StorageConfigurationException(string message) : base(message)
    {
    }

    public StorageConfigurationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
