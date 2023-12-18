using DatasetFileUpload.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Storage;

public interface IStorageService
{
    public Task<StreamWithLength?> GetFileData(DatasetVersionIdentifier datasetVersion, string fileName);

    public Task<RoCrateFile> StoreFile(DatasetVersionIdentifier datasetVersion, string fileName, Stream stream);

    public Task DeleteFile(DatasetVersionIdentifier datasetVersion, string fileName);

    public IAsyncEnumerable<RoCrateFile> ListFiles(DatasetVersionIdentifier datasetVersion);
}