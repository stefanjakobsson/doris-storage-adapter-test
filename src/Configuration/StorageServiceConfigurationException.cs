using System;

namespace DorisStorageAdapter.Configuration;

internal sealed class StorageServiceConfigurationException : Exception
{
    public StorageServiceConfigurationException() : base()
    {
    }

    public StorageServiceConfigurationException(string message) : base(message)
    {
    }

    public StorageServiceConfigurationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
