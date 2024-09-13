namespace DorisStorageAdapter.Services.Exceptions;

internal sealed class ConflictException : ApiException
{
    public ConflictException() : base("Write conflict.", 409) { }
}
