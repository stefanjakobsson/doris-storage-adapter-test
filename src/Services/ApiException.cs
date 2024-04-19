using System;

namespace DatasetFileUpload.Services;

internal abstract class ApiException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
