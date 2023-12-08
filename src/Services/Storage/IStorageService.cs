namespace DatasetFileUpload.Services.Storage;

using DatasetFileUpload.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

public interface IStorageService
{
    public Task<Stream?> GetFileData(string datasetIdentifier, string versionNumber, string fileName);

    public Task<RoCrateFile> StoreFile(string datasetIdentifier, string versionNumber, string fileName, Stream stream);

    public Task DeleteFile(string datasetIdentifier, string versionNumber, string fileName);

    public IAsyncEnumerable<RoCrateFile> ListFiles(string datasetIdentifier, string versionNumber);
}