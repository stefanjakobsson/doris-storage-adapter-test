namespace DatasetFileUpload.Services.Storage;

using System.Text.Json;
using DatasetFileUpload.Models;

interface IStorageService{


    // change to exception
    public Task StoreManifest(string datasetIdentifier, string versionNumber, JsonDocument manifest);


    public Task<JsonDocument> GetManifest(string datasetIdentifier, string versionNumber);

    public Task<RoCrateFile> StoreFile(string datasetIdentifier, string versionNumber, UploadType type, IFormFile file, bool generateFileUrl);

    // change to exception
    public Task DeleteFile(string datasetIdentifier, string versionNumber, UploadType type, string filePath);
}