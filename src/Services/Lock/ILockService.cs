using DatasetFileUpload.Models;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Lock;

public interface ILockService
{
    Task<bool> LockFilePath(DatasetVersionIdentifier datasetVersion, string filePath);

    Task<bool> TryLockFilePath(DatasetVersionIdentifier datasetVersion, string filePath);

    Task ReleaseFilePathLock(DatasetVersionIdentifier datasetVersion, string filePath);

    Task<bool> TryLockDatasetVersion(DatasetVersionIdentifier datasetVersion);

    Task ReleaseDatasetVersionLock(DatasetVersionIdentifier datasetVersion);
}
