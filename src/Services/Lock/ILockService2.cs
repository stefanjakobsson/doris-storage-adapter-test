using DatasetFileUpload.Models;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Lock;

public interface ILockService2
{
    Task<FilePathLockReleaser2> LockFilePath(DatasetVersionIdentifier datasetVersion, string filePath);

    Task<FilePathLockReleaser2> TryLockFilePath(DatasetVersionIdentifier datasetVersion, string filePath);

    Task<FilePathLockReleaser2> TryLockDatasetVersion(DatasetVersionIdentifier datasetVersion);
}
