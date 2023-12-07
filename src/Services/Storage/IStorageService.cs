namespace DatasetFileUpload.Services.Storage;

using DatasetFileUpload.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

public interface IStorageService
{
    public Task StoreRoCrateMetadata(string datasetIdentifier, string versionNumber, string metadata);

    public Task<string?> GetRoCrateMetadata(string datasetIdentifier, string versionNumber);

    public Task<RoCrateFile> StoreFile(string datasetIdentifier, string versionNumber, UploadType type, string fileName, Stream stream);

    public Task DeleteFile(string datasetIdentifier, string versionNumber, UploadType type, string fileName);

    public IAsyncEnumerable<RoCrateFile> ListFiles(string datasetIdentifier, string versionNumber);
}