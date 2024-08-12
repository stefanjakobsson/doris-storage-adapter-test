using AsyncKeyedLock;
using DorisStorageAdapter.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services.Lock;

internal class InProcessLockService : ILockService
{
    private readonly AsyncKeyedLocker<DatasetVersionIdentifier> datasetVersionSharedLocks = new(new AsyncKeyedLockOptions(maxCount: int.MaxValue));
    private readonly AsyncKeyedLocker<DatasetVersionIdentifier> datasetVersionExclusiveLocks = new();
    private readonly AsyncKeyedLocker<string> pathLocks = new();

    public async Task<IDisposable> LockPath(string path, CancellationToken cancellationToken)
    {
        return await pathLocks.LockAsync(path, cancellationToken);
    }

    public async Task<bool> TryLockPath(
        string path, 
        Func<Task> task, 
        CancellationToken cancellationToken)
    {
        return await pathLocks.TryLockAsync(path, task, 0, cancellationToken);
    }

    public async Task<bool> TryLockDatasetVersionExclusive(
        DatasetVersionIdentifier datasetVersion, 
        Func<Task> task, 
        CancellationToken cancellationToken)
    {
        bool noSharedLocks = true;

        bool lockSuccessful = await datasetVersionExclusiveLocks.TryLockAsync(datasetVersion, async () =>
        {
            if (datasetVersionSharedLocks.IsInUse(datasetVersion))
            {
                noSharedLocks = false;
                return;
            }

            await task();
        },
        millisecondsTimeout: 0,
        cancellationToken);

        return lockSuccessful && noSharedLocks;
    }

    public async Task<bool> TryLockDatasetVersionShared(
        DatasetVersionIdentifier datasetVersion, 
        Func<Task> task, 
        CancellationToken cancellationToken)
    {
        using (await datasetVersionSharedLocks.LockAsync(datasetVersion, cancellationToken))
        {
            if (datasetVersionExclusiveLocks.IsInUse(datasetVersion))
            {
                return false;
            }

            await task();
            return true;
        }
    }
}
