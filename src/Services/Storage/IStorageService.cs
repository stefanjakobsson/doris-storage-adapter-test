using DatasetFileUpload.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Storage;

public interface IStorageService
{
    public Task<StreamWithLength?> GetFileData(DatasetVersionIdentifier datasetVersion, string filePath);

    public Task<RoCrateFile> StoreFile(DatasetVersionIdentifier datasetVersion, string filePath, Stream stream);

    public Task DeleteFile(DatasetVersionIdentifier datasetVersion, string filePath);

    public IAsyncEnumerable<RoCrateFile> ListFiles(DatasetVersionIdentifier datasetVersion);
}