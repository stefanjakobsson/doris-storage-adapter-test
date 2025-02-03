namespace DorisStorageAdapter.Services.Contract.Models;

public sealed record ByteRange(
    long? From,
    long? To);