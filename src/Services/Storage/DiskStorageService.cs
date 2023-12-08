namespace DatasetFileUpload.Services.Storage;

using DatasetFileUpload.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

// TODO How handle illegal file name characters, which are different on Windows/Linux?

internal class DiskStorageService(IConfiguration configuration) : IStorageService
{
    private readonly IConfiguration configuration = configuration;

    public Task<Stream?> GetFileData(DatasetVersionIdentifier datasetVersion, string fileName)
    {
        string basePath = GetDatasetVersionPath(datasetVersion);
        string filePath = GetFilePathOrThrow(fileName, basePath);

        if (!File.Exists(filePath))
        {
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(new FileStream(filePath, FileMode.Open, FileAccess.Read));
    }

    public async Task<RoCrateFile> StoreFile(DatasetVersionIdentifier datasetVersion, string fileName, Stream data)
    {
        string basePath = GetDatasetVersionPath(datasetVersion);
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
            Id = NormalizePath(Path.GetRelativePath(basePath, filePath)), 
            ContentSize = fileInfo.Length,
            DateCreated = fileInfo.CreationTime.ToUniversalTime(),
            DateModified = fileInfo.LastWriteTime.ToUniversalTime(),
            EncodingFormat = null,
            Sha256 = null,
            Url = null
        };
    }

    public Task DeleteFile(DatasetVersionIdentifier datasetVersion, string fileName)
    {
        string basePath = GetDatasetVersionPath(datasetVersion);
        string filePath = GetFilePathOrThrow(fileName, basePath);

        if (!File.Exists(filePath))
        {
            return Task.CompletedTask;
        }

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

    public async IAsyncEnumerable<RoCrateFile> ListFiles(DatasetVersionIdentifier datasetVersion)
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

        string basePath = GetDatasetVersionPath(datasetVersion);

        IEnumerable<FileInfo> EnumerateFilesForType(UploadType type) =>
            EnumerateFiles(Path.Combine(basePath, GetPathForType(type)))
            .OrderBy(f => f.FullName, StringComparer.InvariantCulture);

        foreach (var type in new[] { UploadType.Data, UploadType.Documentation })
        {
            foreach (var file in EnumerateFilesForType(type))
            {
                var relativePath = Path.GetRelativePath(basePath, file.FullName);

                yield return new()
                {
                    Id = NormalizePath(relativePath),
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

    private string GetDatasetVersionPath(DatasetVersionIdentifier datasetVersion) =>
        Path.GetFullPath(Path.Combine(
            GetBasePath(), datasetVersion.DatasetIdentifier, datasetVersion.DatasetIdentifier + '-' + datasetVersion.VersionNumber));

    private static string GetFilePathOrThrow(string fileName, string basePath)
    {
        string filePath = Path.GetFullPath(fileName, basePath);

        if (!filePath.StartsWith(basePath))
        {
            throw new IllegalFileNameException(fileName);
        }

        return filePath;
    }

    private static string NormalizePath(string path)
    {
        if (Path.DirectorySeparatorChar != '/')
        {
            return path.Replace(Path.DirectorySeparatorChar, '/');
        }

        return path;
    }
}