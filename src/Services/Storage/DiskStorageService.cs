namespace DatasetFileUpload.Services.Storage;

using DatasetFileUpload.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
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
        Stream data)
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

    public async IAsyncEnumerable<RoCrateFile> ListFiles(string datasetIdentifier, string versionNumber)
    {
        // This is a hack to avoid warning CS1998 (async method without await)
        await Task.CompletedTask;

        static IEnumerable<FileInfo> EnumerateFiles(string path)
        {
            var directory = new DirectoryInfo(path);

            if (!directory.Exists)
            {
                yield break;
            }

            foreach (var file in directory.EnumerateFiles())
            {
                yield return file;
            }

            foreach (var subDirectory in directory.EnumerateDirectories())
            {
                foreach (var file in EnumerateFiles(subDirectory.FullName))
                {
                    yield return file;
                }
            }
        }

        string basePath = GetDatasetVersionPath(datasetIdentifier, versionNumber);

        IEnumerable<FileInfo> EnumerateFilesForType(UploadType type) =>
            EnumerateFiles(Path.Combine(basePath, GetPathForType(type)))
            .OrderBy(f => f.FullName, StringComparer.InvariantCulture);

        foreach (var type in new[] { UploadType.Data, UploadType.Documentation })
        {
            foreach (var file in EnumerateFilesForType(type))
            {
                var relativePath = Path.GetRelativePath(basePath, file.FullName);

                if (Path.DirectorySeparatorChar != '/')
                {
                    relativePath = relativePath.Replace(Path.DirectorySeparatorChar, '/');
                }

                yield return new()
                {
                    Id = relativePath,
                    ContentSize = file.Length,
                    DateCreated = file.CreationTime.ToUniversalTime(),
                    DateModified = file.LastWriteTime.ToUniversalTime(),
                    EncodingFormat = null,
                    Sha256 = null,
                    Url = null
                };
            }
        }
    }

    private static string GetPathForType(UploadType type) => type.ToString().ToLower();

    private string GetBasePath() => Path.GetFullPath(configuration["Storage:DiskStorageService:BasePath"]!);

    private string GetDatasetVersionPath(string datasetIdentifier, string versionNumber) =>
        Path.GetFullPath(Path.Combine(GetBasePath(), datasetIdentifier + '-' + versionNumber));

    private string GetDatasetVersionPath(string datasetIdentifier, string versionNumber, UploadType type) =>
       Path.Combine(GetDatasetVersionPath(datasetIdentifier, versionNumber), GetPathForType(type));

    private string GetRoCrateMetadataPath(string datasetIdentifier, string versionNumber) =>
        Path.Combine(GetDatasetVersionPath(datasetIdentifier, versionNumber), roCrateMetadataFileName);

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