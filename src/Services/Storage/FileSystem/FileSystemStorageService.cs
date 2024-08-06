using DorisStorageAdapter.Services.Exceptions;
using DorisStorageAdapter.Services.Lock;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services.Storage.FileSystem;

internal class FileSystemStorageService(
    IOptions<FileSystemStorageServiceConfiguration> configuration,
    ILockService lockService) : IStorageService
{
    private static readonly HashSet<char> invalidFileNameChars = [.. Path.GetInvalidFileNameChars()];

    private readonly ILockService lockService = lockService;

    private readonly string basePath = Path.GetFullPath(configuration.Value.BasePath);
    private readonly string tempFilePath = Path.GetFullPath(configuration.Value.TempFilePath);

    public async Task<StorageServiceFileBase> StoreFile(string filePath, FileData data)
    {
        string lockPath = GetPathRoot(filePath);
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

        using (await lockService.LockPath(lockPath))
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
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

    public async Task DeleteFile(string filePath)
    {
        string lockPath = GetPathRoot(filePath);
        filePath = GetPathOrThrow(filePath, basePath);

        if (!File.Exists(filePath))
        {
            return;
        }

        File.Delete(filePath);

        // Delete any empty subdirectories that result from deleting the file
        using (await lockService.LockPath(lockPath))
        {
            string fullBasePath = Path.GetFullPath(basePath);
            DirectoryInfo? directory = new(Path.GetDirectoryName(filePath)!);
            while
            (
                directory != null &&
                directory.FullName != fullBasePath &&
                !directory.EnumerateFileSystemInfos().Any()
            )
            {
                directory.Delete(false);
                directory = directory.Parent;
            }
        }
    }

    public Task<FileData?> GetFileData(string filePath)
    {
        filePath = GetPathOrThrow(filePath, basePath);

        if (!File.Exists(filePath))
        {
            return Task.FromResult<FileData?>(null);
        }

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return Task.FromResult<FileData?>(new(
            Stream: stream,
            Length: stream.Length,
            ContentType: null));
    }

    public async IAsyncEnumerable<StorageServiceFile> ListFiles(string path)
    {
        // This is a hack to avoid warning CS1998 (async method without await)
        await Task.CompletedTask;

        static IEnumerable<FileInfo> EnumerateFiles(DirectoryInfo directory, string path)
        {
            foreach (var entry in directory.EnumerateFileSystemInfos())
            {
                if (path != "" && !entry.FullName.StartsWith(path))
                {
                    continue;
                }

                if (entry is DirectoryInfo subDirectory)
                {
                    foreach (var file in EnumerateFiles(subDirectory, ""))
                    {
                        yield return file;
                    }
                }
                else if (entry is FileInfo file)
                {
                    yield return file;
                }
            }
        }


        path = GetPathOrThrow(path, basePath);
        var directory = new DirectoryInfo(path);

        if (!directory.Exists)
        {
            directory = new(path[..path.LastIndexOf(Path.DirectorySeparatorChar)]);

            if (!directory.Exists)
            {
                yield break;
            }
        }

        foreach (var file in EnumerateFiles(directory, directory.FullName != path ? path : ""))
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

    private static string GetPathRoot(string path)
    {
        int index = path.IndexOf('/') + 1;
        if (index > 0)
        {
            return path[..index];
        }

        return path;
    }
}