using AsyncKeyedLock;
using DatasetFileUpload.Models;
using System;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Lock;

internal class InProcessLockService : ILockService
{
    private readonly AsyncKeyedLocker<DatasetVersionIdentifier> datasetVersionActiveFilePathLocks = new(new AsyncKeyedLockOptions(maxCount: int.MaxValue));
    private readonly AsyncKeyedLocker<DatasetVersionIdentifier> datasetVersionLocks = new();
    private readonly AsyncKeyedLocker<(DatasetVersionIdentifier DatasetVersion, string FilePath)> filePathLocks = new();

    public async Task<bool> LockFilePath(DatasetVersionIdentifier datasetVersion, string filePath, Func<Task> task)
    {
        using (await datasetVersionActiveFilePathLocks.LockAsync(datasetVersion))
        {
            if (datasetVersionLocks.IsInUse(datasetVersion))
            {
                return false;
            }

            using (await filePathLocks.LockAsync((datasetVersion, filePath)))
            {
                await task();
                return true;
            }
        }
    }

    public async Task<bool> TryLockFilePath(DatasetVersionIdentifier datasetVersion, string filePath, Func<Task> task)
    {
        using (await datasetVersionActiveFilePathLocks.LockAsync(datasetVersion))
        {
            if (datasetVersionLocks.IsInUse(datasetVersion))
            {
                return false;
            }

            return await filePathLocks.TryLockAsync((datasetVersion, filePath), task, 0);
        }
    }


    public async Task<bool> TryLockDatasetVersion(DatasetVersionIdentifier datasetVersion, Func<Task> task)
    {
        bool noFilePathLocks = true;

        bool lockSuccessful = await datasetVersionLocks.TryLockAsync(datasetVersion, async () =>
        {
            if (datasetVersionActiveFilePathLocks.IsInUse(datasetVersion))
            {
                noFilePathLocks = false;
                return;
            }

            await task();
        }, 
        millisecondsTimeout: 0);

        return lockSuccessful && noFilePathLocks;
    }
}
