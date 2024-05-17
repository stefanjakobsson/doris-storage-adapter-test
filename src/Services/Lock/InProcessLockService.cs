using AsyncKeyedLock;
using DatasetFileUpload.Models;
using System;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Lock;

internal class InProcessLockService : ILockService
{
    private readonly AsyncKeyedLocker<DatasetVersionIdentifier> datasetVersionSharedLocks = new(new AsyncKeyedLockOptions(maxCount: int.MaxValue));
    private readonly AsyncKeyedLocker<DatasetVersionIdentifier> datasetVersionExclusiveLocks = new();
    private readonly AsyncKeyedLocker<string> pathLocks = new();

    public async Task<IDisposable> LockPath(string path)
    {
        return await pathLocks.LockAsync(path);
    }

    public async Task<bool> TryLockPath(string path, Func<Task> task)
    {
        return await pathLocks.TryLockAsync(path, task, 0);
    }

    public async Task<bool> TryLockDatasetVersionExclusive(DatasetVersionIdentifier datasetVersion, Func<Task> task)
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
        millisecondsTimeout: 0);

        return lockSuccessful && noSharedLocks;
    }

    public async Task<bool> TryLockDatasetVersionShared(DatasetVersionIdentifier datasetVersion, Func<Task> task)
    {
        using (await datasetVersionSharedLocks.LockAsync(datasetVersion))
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
