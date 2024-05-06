using DatasetFileUpload.Services.Exceptions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Storage.FileSystem;

internal class FileSystemStorageService(IOptions<FileSystemStorageServiceConfiguration> configuration) : IStorageService
{
    private static readonly HashSet<char> invalidFileNameChars = [.. Path.GetInvalidFileNameChars()];

    private readonly string basePath = Path.GetFullPath(configuration.Value.BasePath);
    private readonly string tempFilePath = Path.GetFullPath(configuration.Value.TempFilePath);
  
    public async Task<StorageServiceFileBase> StoreFile(string filePath, FileData data)
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
            await data.Stream.CopyToAsync(stream);
        }

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var fileInfo = new FileInfo(filePath);

        DateTime? dateCreated = null;
        if (fileInfo.Exists)
        {
            dateCreated = fileInfo.CreationTimeUtc;
        }

        File.Move(tempFile, filePath, true);

        fileInfo.Refresh();
        if (dateCreated != null)
        {
            fileInfo.CreationTimeUtc = dateCreated.Value;
        }

        return new(
            ContentType: null,
            DateCreated: dateCreated ?? fileInfo.CreationTimeUtc,
            DateModified: fileInfo.LastWriteTimeUtc);
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

    public Task<FileData?> GetFileData(string filePath)
    {
        filePath = GetPathOrThrow(filePath, basePath);

        if (!File.Exists(filePath))
        {
            return Task.FromResult<FileData?>(null);
        }

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return Task.FromResult<FileData?>(new(stream, stream.Length, null));
    }

    public async IAsyncEnumerable<StorageServiceFile> ListFiles(string path)
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

        foreach (var file in EnumerateFiles(path))
        {
            var relativePath = Path.GetRelativePath(basePath, file.FullName);

            yield return new(
                ContentType: null,
                DateCreated: file.CreationTimeUtc,
                DateModified: file.LastWriteTimeUtc,
                Path: NormalizePath(relativePath),
                Length: file.Length);
        }
    }

    private static string GetPathOrThrow(string path, string basePath)
    {
        void Throw() => throw new IllegalPathException(path);

        if (path.Split('/').Any(c => c.Any(invalidFileNameChars.Contains)))
        {
            Throw();
        }

        string result = Path.GetFullPath(path, basePath);

        if (!result.StartsWith(basePath))
        {
            Throw();
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