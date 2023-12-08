namespace DatasetFileUpload.Services.Storage;

using DatasetFileUpload.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

public interface IStorageService
{
    public Task<Stream?> GetFileData(DatasetVersionIdentifier datasetVersion, string fileName);

    public Task<RoCrateFile> StoreFile(DatasetVersionIdentifier datasetVersion, string fileName, Stream stream);

    public Task DeleteFile(DatasetVersionIdentifier datasetVersion, string fileName);

    public IAsyncEnumerable<RoCrateFile> ListFiles(DatasetVersionIdentifier datasetVersion);
}