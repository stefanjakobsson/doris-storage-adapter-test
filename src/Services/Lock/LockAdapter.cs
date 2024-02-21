using DatasetFileUpload.Models;
using System;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Lock;

public class LockAdapter(ILockService lockService)
{
    private readonly ILockService lockService = lockService;

    /*public async Task<bool> LockFilePath(DatasetVersionIdentifier datasetVersion, string filePath, Func<Task> action)
    {
        if (!await lockService.LockFilePath(datasetVersion, filePath))
        {
            return false;
        }

        try
        {
            await action();
        }
        finally
        {
            await lockService.ReleaseFilePathLock(datasetVersion, filePath);
        }

        return true;
    }

    public async Task<(T? Result, bool Success)> TryLockFilePath<T>(DatasetVersionIdentifier datasetVersion, string filePath, Func<Task<T>> action)
    {
        if (!await lockService.TryLockFilePath(datasetVersion, filePath))
        {
            return (default, false);
        }

        T result;
        try
        {
            result = await action();
        }
        finally
        {
            await lockService.ReleaseFilePathLock(datasetVersion, filePath);
        }

        return ;
    }

    public async Task<bool> TryLockDatasetVersion(DatasetVersionIdentifier datasetVersion, Func<Task> action)
    {
        if (!await lockService.TryLockDatasetVersion(datasetVersion))
        {
            return false;
        }

        try
        {
            await action();
        }
        finally
        {
            await lockService.ReleaseDatasetVersionLock(datasetVersion);
        }

        return true;
    }*/
}
