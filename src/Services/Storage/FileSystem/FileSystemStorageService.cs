using DatasetFileUpload.Models;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Storage.Disk;

// TODO How handle illegal file name characters, which are different on Windows/Linux?

internal class FileSystemStorageService(IOptions<FileSystemStorageServiceConfiguration> configuration) : IStorageService
{
    private readonly string basePath = Path.GetFullPath(configuration.Value.BasePath);
    private readonly string tempFilePath = Path.GetFullPath(configuration.Value.TempFilePath);

    public Task<StreamWithLength?> GetFileData(string filePath)
    {
        filePath = GetPathOrThrow(filePath, basePath);

        if (!File.Exists(filePath))
        {
            return Task.FromResult<StreamWithLength?>(null);
        }

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return Task.FromResult<StreamWithLength?>(new(stream, stream.Length));
    }

    public async Task<RoCrateFile> StoreFile(string filePath, Stream data)
    {
        filePath = GetPathOrThrow(filePath, basePath);
        string directoryPath = Path.GetDirectoryName(filePath)!;

        string tempFile;
        do
        {
            tempFile = Path.Combine(tempFilePath, Path.GetRandomFileName());
        }
        while (File.Exists(tempFile));

        using (var stream = new FileStream(tempFile, FileMode.Create))
        {
            await data.CopyToAsync(stream);
        }

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        File.Move(tempFile, filePath, true);

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

    public Task DeleteFile(string filePath)
    {
        filePath = GetPathOrThrow(filePath, basePath);

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

    public async IAsyncEnumerable<RoCrateFile> ListFiles(string path)
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

        path = GetPathOrThrow(path, basePath);

        foreach (var file in EnumerateFiles(path)
            .OrderBy(f => f.FullName, StringComparer.InvariantCulture))
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

    private static string GetPathOrThrow(string path, string basePath)
    {
        string result = Path.GetFullPath(path, basePath);

        if (!result.StartsWith(basePath))
        {
            throw new IllegalPathException(path);
        }

        return result;
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