using System;

namespace DorisStorageAdapter.Services.Exceptions;

internal abstract class ApiException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
