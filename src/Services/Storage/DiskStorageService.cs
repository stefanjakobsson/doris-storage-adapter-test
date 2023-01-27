using DatasetFileUpload.Services.Storage;
using System.Text.Json;
using System.Text.Json.Serialization;
using DatasetFileUpload.Models;

class DiskStorageService : IStorageService
{
    private readonly IConfiguration configuration;

    public DiskStorageService(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

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
        if(File.Exists(path)){
            using FileStream stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<JsonDocument>(stream);
        }

        throw new FileNotFoundException();
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