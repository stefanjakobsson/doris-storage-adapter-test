using DorisStorageAdapter.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services.Lock;

internal interface ILockService
{
    Task<IDisposable> LockPath(
        string path, 
        CancellationToken cancellationToken);

    Task<bool> TryLockPath(
        string path, 
        Func<Task> task, 
        CancellationToken cancellationToken);

    Task<bool> TryLockDatasetVersionExclusive(
        DatasetVersion datasetVersion, 
        Func<Task> task, 
        CancellationToken cancellationToken);

    Task<bool> TryLockDatasetVersionShared(
        DatasetVersion datasetVersion, 
        Func<Task> task, 
        CancellationToken cancellationToken);
}
