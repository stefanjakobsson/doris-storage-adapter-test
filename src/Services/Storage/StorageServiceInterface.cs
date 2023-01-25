namespace DatasetFileUpload.Services.Storage;

using System.Text.Json;
using DatasetFileUpload.Models;

interface IStorageService{

    public bool StoreManifest(string datasetIdentifier, string versionNumber, JsonDocument manifest);

    public RoCrateFile StoreFile(string datasetIdentifier, string versionNumber, FileType type, IFormFile file);

    public bool DeleteFile(string datasetIdentifier, string versionNumber, FileType type, string filePath);
}