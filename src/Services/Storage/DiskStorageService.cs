using DatasetFileUpload.Services.Storage;
using System.Text.Json;
using System.Text.Json.Serialization;
using DatasetFileUpload.Models;

class DiskStorageService : IStorageService
{

    public async Task StoreManifest(string datasetIdentifier, string versionNumber, JsonDocument manifest)
    {
        string path = Path.Combine("/var/data", datasetIdentifier, datasetIdentifier + '-' + versionNumber, "metadata", "ro-crate-metadata-json");

        var fi = new FileInfo(path);
        fi.Directory?.Create();

        using var fileStream = new FileStream(path, FileMode.Create);
        await JsonSerializer.SerializeAsync(fileStream, manifest);
    }

    public async Task<JsonDocument> GetManifest(string datasetIdentifier, string versionNumber)
    {
        throw new NotImplementedException();
    }

    public async Task<RoCrateFile> StoreFile(string datasetIdentifier, string versionNumber, UploadType type, IFormFile file, bool generateFileUrl)
    {
        // move base path to config
        string path = Path.Combine("/var/data", datasetIdentifier, datasetIdentifier + '-' + versionNumber, type.ToString().ToLower(), file.FileName);
        var fi = new FileInfo(path);
        fi.Directory?.Create();

        using var fileStream = new FileStream(path, FileMode.Create);

        await file.CopyToAsync(fileStream);

        return new RoCrateFile{
            Id = type.ToString().ToLower() + '/' + file.FileName, 
            ContentSize = file.Length,
            DateCreated = fi.LastWriteTime
        };
    }

    public async Task DeleteFile(string datasetIdentifier, string versionNumber, UploadType type, string filePath)
    {
        throw new NotImplementedException();
    }

}