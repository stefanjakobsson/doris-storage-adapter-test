using DatasetFileUpload.Services.Storage;
using System.Text.Json;
using System.Text.Json.Serialization;
using DatasetFileUpload.Models;

class DiskStorageService : IStorageService
{

    public bool StoreManifest(string datasetIdentifier, string versionNumber, JsonDocument manifest)
    {
        string path = Path.Combine("/var/data", datasetIdentifier, datasetIdentifier + '-' + versionNumber, "metadata", "ro-crate-metadata-json");

        var fi = new FileInfo(path);
        fi.Directory?.Create();

        using var fileStream = new FileStream(path, FileMode.Create);
        JsonSerializer.SerializeAsync(fileStream, manifest);

        return true;
    }

    public RoCrateFile StoreFile(string datasetIdentifier, string versionNumber, FileType type, IFormFile file)
    {
        string path = Path.Combine("/var/data", datasetIdentifier, datasetIdentifier + '-' + versionNumber, type.ToString().ToLower(), file.FileName);
        var fi = new FileInfo(path);
        fi.Directory?.Create();

        using var fileStream = new FileStream(path, FileMode.Create);

        file.CopyToAsync(fileStream);

        return new RoCrateFile{
            Id = type.ToString().ToLower() + '/' + file.FileName, 
            ContentSize = file.Length,
            DateCreated = fi.LastWriteTime
        };
    }

    public bool DeleteFile(string datasetIdentifier, string versionNumber, FileType type, string filePath)
    {
        throw new NotImplementedException();
    }

}