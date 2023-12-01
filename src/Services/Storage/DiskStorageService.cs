namespace DatasetFileUpload.Services.Storage;

using DatasetFileUpload.Models;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal class DiskStorageService : IStorageService
{
    private const string roCrateMetadataFileName = "ro-crate-metadata.json";

    private readonly IConfiguration configuration;

    public DiskStorageService(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public async Task StoreRoCrateMetadata(string datasetIdentifier, string versionNumber, string metadata)
    {
        string path = GetRoCrateMetadataPath(datasetIdentifier, versionNumber);

        await File.WriteAllTextAsync(path, metadata, new UTF8Encoding(false));
    }

    public async Task<string?> GetRoCrateMetadata(string datasetIdentifier, string versionNumber)
    {
        string path = GetRoCrateMetadataPath(datasetIdentifier, versionNumber);

        if (File.Exists(path))
        {
            return await File.ReadAllTextAsync(path, new UTF8Encoding(false));
        }

        return null;
    }

    public async Task<RoCrateFile> StoreFile(
        string datasetIdentifier, 
        string versionNumber,
        UploadType type, 
        string fileName, 
        Stream data, 
        bool generateFileUrl)
    {
        string basePath = GetDatasetVersionPath(datasetIdentifier, versionNumber, type);
        string filePath = GetFilePathOrThrow(fileName, basePath);
        string directoryPath = Path.GetDirectoryName(filePath)!;

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        using var stream = new FileStream(filePath, FileMode.Create);
        await data.CopyToAsync(stream);

        var fileInfo = new FileInfo(filePath);

        return new RoCrateFile
        {
            Id = Path.GetRelativePath(basePath, filePath), 
            ContentSize = fileInfo.Length,
            DateCreated = fileInfo.CreationTime.ToUniversalTime(),
            DateModified = fileInfo.LastWriteTime.ToUniversalTime(),
            EncodingFormat = null,
            Sha256 = null,
            Url = null
        };
    }

    public Task DeleteFile(
        string datasetIdentifier, 
        string versionNumber, 
        UploadType type, 
        string fileName)
    {
        string basePath = GetDatasetVersionPath(datasetIdentifier, versionNumber, type);
        string filePath = GetFilePathOrThrow(fileName, basePath);

        File.Delete(filePath);

        // Delete any empty subdirectories that result from deleting the file
        DirectoryInfo? directory = new(Path.GetDirectoryName(filePath)!);
        while 
        (
            directory != null &&
            directory.FullName != basePath &&
            !directory.EnumerateFiles("*", SearchOption.TopDirectoryOnly).Any()
        )
        {
            directory.Delete(false);
            directory = directory.Parent;
        }

        return Task.CompletedTask;
    }

    private string GetBasePath() => Path.GetFullPath(configuration["Storage:DiskStorageService:BasePath"]!);

    private string GetDatasetVersionPath(string datasetIdentifier, string versionNumber) =>
        Path.GetFullPath(Path.Combine(GetBasePath(), datasetIdentifier + '-' + versionNumber));

    private string GetDatasetVersionPath(string datasetIdentifier, string versionNumber, UploadType type) =>
       Path.Combine(GetDatasetVersionPath(datasetIdentifier, versionNumber), type.ToString().ToLower());

    private string GetRoCrateMetadataPath(string datasetIdentifier, string versionNumber) =>
        Path.Combine(GetDatasetVersionPath(datasetIdentifier, versionNumber, UploadType.Metadata), roCrateMetadataFileName);

    private static string GetFilePathOrThrow(string fileName, string basePath)
    {
        var filePath = Path.GetFullPath(fileName, basePath);

        if (!filePath.StartsWith(basePath))
        {
            throw new IllegalFileNameException(fileName);
        }

        return filePath;
    }
}