namespace DorisStorageAdapter.Services.Exceptions;

internal class ConflictException : ApiException
{
    public ConflictException() : base("Write conflict.", 409) { }
}
