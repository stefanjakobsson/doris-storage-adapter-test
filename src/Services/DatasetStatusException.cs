using System;

namespace DatasetFileUpload.Services;

internal class DatasetStatusException : ApiException
{
    public DatasetStatusException() : base("Status mismatch.") { }
}
