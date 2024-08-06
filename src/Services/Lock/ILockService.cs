using DorisStorageAdapter.Models;
using System;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services.Lock;

public interface ILockService
{
    Task<IDisposable> LockPath(string path);

    Task<bool> TryLockPath(string path, Func<Task> task);

    Task<bool> TryLockDatasetVersionExclusive(DatasetVersionIdentifier datasetVersion, Func<Task> task);

    Task<bool> TryLockDatasetVersionShared(DatasetVersionIdentifier datasetVersion, Func<Task> task);
}
