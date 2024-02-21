using DatasetFileUpload.Models;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Lock;

internal class InProcessLockService : ILockService
{
    private readonly object lockObject = new();

    private readonly Dictionary<DatasetVersionIdentifier, SemaphoreSlim> datasetVersionLocks = [];
    private readonly Dictionary<DatasetVersionIdentifier, Dictionary<string, SemaphoreSlim>> datasetVersionPathLocks = [];

    public async Task<bool> LockFilePath(DatasetVersionIdentifier datasetVersion, string filePath)
    {
        if (TryGetPathSemaphore(datasetVersion, filePath, out var semaphore))
        {
            await semaphore.WaitAsync();
            return true;
        }

        return false;
    }

    public Task<bool> TryLockFilePath(DatasetVersionIdentifier datasetVersion, string filePath)
    {
        if (TryGetPathSemaphore(datasetVersion, filePath, out var semaphore))
        {
            return semaphore.WaitAsync(0);
        }

        return Task.FromResult(false);
    }

    private bool TryGetPathSemaphore(DatasetVersionIdentifier datasetVersion, string filePath, [NotNullWhen(true)] out SemaphoreSlim? semaphore)
    {
        lock (lockObject)
        {
            if (datasetVersionLocks.TryGetValue(datasetVersion, out var versionLock) &&
                versionLock.CurrentCount == 0)
            {
                semaphore = null;
                return false;
            }

            if (!datasetVersionPathLocks.TryGetValue(datasetVersion, out var pathLocks) ||
                !pathLocks.TryGetValue(filePath, out semaphore))
            {
                semaphore = new SemaphoreSlim(1);
                pathLocks ??= [];
                pathLocks[filePath] = semaphore;
            }

            return true;
        }
    }


    public Task ReleaseFilePathLock(DatasetVersionIdentifier datasetVersion, string filePath)
    {
        lock (lockObject)
        {
            if (datasetVersionPathLocks.TryGetValue(datasetVersion, out var pathLocks) &&
                pathLocks.TryGetValue(filePath, out var semaphore))
            {
                semaphore.Release();
            }
        }

        return Task.CompletedTask;
    }

    public Task<bool> TryLockDatasetVersion(DatasetVersionIdentifier datasetVersion)
    {
        SemaphoreSlim semaphore;
        lock (lockObject)
        {
            if (datasetVersionPathLocks.TryGetValue(datasetVersion, out var pathLocks) &&
                pathLocks.Values.Any(semaphore => semaphore.CurrentCount == 0))
            {
                return Task.FromResult(false);
            }

            if (!datasetVersionLocks.TryGetValue(datasetVersion, out var result))
            {
                result = new SemaphoreSlim(1);
                datasetVersionLocks[datasetVersion] = result;
            }

            semaphore = result;
        }


        return semaphore.WaitAsync(0);
    }

    public Task ReleaseDatasetVersionLock(DatasetVersionIdentifier datasetVersion)
    {
        lock (lockObject)
        {
            if (datasetVersionLocks.TryGetValue(datasetVersion, out var semaphore))
            {
                semaphore.Release();
            }
        }

        return Task.CompletedTask;
    }
}
