using System.IO;

namespace DorisStorageAdapter.Services.Contract.Models;

public sealed record FileData(
    string? ContentType,
    long Size,
    Stream Stream,
    long StreamLength);