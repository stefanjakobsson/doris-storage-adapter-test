using DatasetFileUpload.Models;
using System;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Lock;

public sealed class FilePathLockReleaser2(
    Func<Task> releaser,
    DatasetVersionIdentifier datasetVersion,
    string filePath) : IAsyncDisposable
{
    private readonly Func<Task> releaser = releaser;
    private readonly DatasetVersionIdentifier datasetVersion = datasetVersion;
    private readonly string filePath = filePath;

    public bool Succeded { get; init; }

    public async ValueTask DisposeAsync()
    {
        await releaser();
    }
}
