using DatasetFileUpload.Models;
using System;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Lock;

public sealed class FilePathLockReleaser(
    ILockService lockService,
    DatasetVersionIdentifier datasetVersion,
    string filePath) : IAsyncDisposable
{
    private readonly ILockService lockService = lockService;
    private readonly DatasetVersionIdentifier datasetVersion = datasetVersion;
    private readonly string filePath = filePath;

    public bool Succeded { get; init; }

    public async ValueTask DisposeAsync()
    {
        await lockService.ReleaseFilePathLock(datasetVersion, filePath);
    }
}
