using System;

namespace DatasetFileUpload.Services;

internal class ConflictException : ApiException
{
    public ConflictException() : base("Write conflict.", 409) { }
}
