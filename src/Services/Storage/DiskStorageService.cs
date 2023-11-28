using DatasetFileUpload.Models;
using DatasetFileUpload.Services.Storage;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

internal class DiskStorageService : IStorageService
{

    public async Task StoreManifest(string datasetIdentifier, string versionNumber, JsonDocument manifest)
    {
        // move base path to config
        string path = Path.Combine("/var/data", datasetIdentifier, datasetIdentifier + '-' + versionNumber, "metadata", "ro-crate-metadata-json");

        var fi = new FileInfo(path);
        fi.Directory?.Create();

        using var fileStream = new FileStream(path, FileMode.Create);
        await JsonSerializer.SerializeAsync(fileStream, manifest);
    }

    public async Task<JsonDocument> GetManifest(string datasetIdentifier, string versionNumber)
    {
        // move base path to config
        string path = Path.Combine("/var/data", datasetIdentifier, datasetIdentifier + '-' + versionNumber, "metadata", "ro-crate-metadata-json");
        if (File.Exists(path))
        {
            using FileStream stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<JsonDocument>(stream) ?? JsonDocument.Parse("{}");
        }

        throw new FileNotFoundException();
    }

    public async Task<RoCrateFile> StoreFile(string datasetIdentifier, string versionNumber, UploadType type, string fileName, Stream stream, bool generateFileUrl)
    {
        // move base path to config
        string path = Path.Combine("/var/data", datasetIdentifier, datasetIdentifier + '-' + versionNumber, type.ToString().ToLower(), fileName);
        var fi = new FileInfo(path);
        fi.Directory?.Create();

        using var fileStream = new FileStream(path, FileMode.Create);

        await stream.CopyToAsync(fileStream);

        

        return new RoCrateFile
        {
            Id = type.ToString().ToLower() + '/' + fileName, 
            ContentSize = fi.Length,
            DateCreated = fi.LastWriteTime
        };
    }

    public async Task DeleteFile(string datasetIdentifier, string versionNumber, UploadType type, string filePath)
    {
        string path = Path.Combine("/var/data", datasetIdentifier, datasetIdentifier + '-' + versionNumber, type.ToString().ToLower(), filePath);
        var fi = new FileInfo(path);
        if (File.Exists(path))
        {
            await Task.Factory.StartNew(() => fi.Delete());
        }
    }

}