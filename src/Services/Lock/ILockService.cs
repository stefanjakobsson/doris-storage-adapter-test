using DatasetFileUpload.Models;
using System;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Lock;

public interface ILockService
{
    Task<bool> LockFilePath(DatasetVersionIdentifier datasetVersion, string filePath, Func<Task> task);

    Task<bool> TryLockFilePath(DatasetVersionIdentifier datasetVersion, string filePath, Func<Task> task);

    Task<bool> TryLockDatasetVersion(DatasetVersionIdentifier datasetVersion, Func<Task> task);
}
