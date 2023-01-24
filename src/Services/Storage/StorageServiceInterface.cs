namespace DatasetFileUpload.Services.Storage;

using DatasetFileUpload.Models;

interface IStorageService{

    public bool storeManifest(string datasetIdentifier, string versionNumber, string manifest);

    public File storeFile(string datasetIdentifier, string versionNumber, FileType type, IFormFile file);

    public bool deleteFile(string datasetIdentifier, string versionNumber, FileType type, string filePath);
}