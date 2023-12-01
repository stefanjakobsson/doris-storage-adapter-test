namespace DatasetFileUpload.Services.Storage;

using DatasetFileUpload.Models;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

public interface IStorageService
{
    public Task StoreManifest(string datasetIdentifier, string versionNumber, JsonDocument manifest);

    public Task<JsonDocument> GetManifest(string datasetIdentifier, string versionNumber);

    public Task<RoCrateFile> StoreFile(string datasetIdentifier, string versionNumber, UploadType type, string fileName, Stream stream, bool generateFileUrl);

    public Task DeleteFile(string datasetIdentifier, string versionNumber, UploadType type, string fileName);
}