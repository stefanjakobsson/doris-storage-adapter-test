using System;

namespace DatasetFileUpload.Services.Exceptions;

internal abstract class ApiException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
