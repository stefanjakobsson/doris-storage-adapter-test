using DorisStorageAdapter.Services.Exceptions;
using DorisStorageAdapter.Services.Lock;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
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

    public async Task<StorageServiceFileBase> StoreFile(
        string filePath, 
        FileData data,
        CancellationToken cancellationToken)
    {
        filePath = GetFullPathOrThrow(filePath);

        string tempFile = Path.Combine(tempFilePath, Guid.NewGuid().ToString());
        string directoryPath = Path.GetDirectoryName(filePath)!;
        FileInfo fileInfo;
        DateTime? dateCreated = null;

        try
        {
            using (var stream = new FileStream(tempFile, FileMode.Create))
            {
                await data.Stream.CopyToAsync(stream, cancellationToken);
            }

            await CreateDirectory(directoryPath, cancellationToken);

            fileInfo = new FileInfo(filePath);
            if (fileInfo.Exists)
            {
                dateCreated = fileInfo.CreationTimeUtc;
            }

            File.Move(tempFile, filePath, true);
        }
        catch
        {
            // Failed, try to clean up
            try
            {
                File.Delete(tempFile);
                await DeleteEmptyDirectories(directoryPath, CancellationToken.None);
            }
            catch { }

            throw;
        }

        try
        {
            // Update creation time if necessary
            fileInfo.Refresh();
            if (dateCreated != null)
            {
                fileInfo.CreationTimeUtc = dateCreated.Value;
            }
        }
        catch 
        {
            // Ignore errors here since file has been successfully stored
            // and updating creation time is not crucial
        }

        return new(
            ContentType: null,
            DateCreated: dateCreated ?? fileInfo.CreationTimeUtc,
            DateModified: fileInfo.LastWriteTimeUtc);
    }

    public async Task DeleteFile(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        filePath = GetFullPathOrThrow(filePath);

        try
        {
            File.Delete(filePath);
        }
        catch (DirectoryNotFoundException)
        { }


        try
        {
            // Delete any empty subdirectories that result from deleting the file
            await DeleteEmptyDirectories(Path.GetDirectoryName(filePath)!, CancellationToken.None);
        }
        catch
        {
            // Ignore errors here since file has been successfully removed
            // and removing empty directories is not crucial
        }
    }

    public Task<FileData?> GetFileData(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        filePath = GetFullPathOrThrow(filePath);

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

#pragma warning disable 1998
    public async IAsyncEnumerable<StorageServiceFile> ListFiles(
        string path,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        static IEnumerable<FileInfo> EnumerateFiles(DirectoryInfo directory, string path)
        {
            foreach (var entry in directory.EnumerateFileSystemInfos())
            {
                if (path != "" && !entry.FullName.StartsWith(path, StringComparison.Ordinal))
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

        cancellationToken.ThrowIfCancellationRequested();

        path = GetFullPathOrThrow(path);
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
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(basePath, file.FullName);

            yield return new(
              ContentType: null,
              DateCreated: file.CreationTimeUtc,
              DateModified: file.LastWriteTimeUtc,
              Path: NormalizePath(relativePath),
              Length: file.Length);
        }
    }
#pragma warning restore 1998

    private string GetFullPathOrThrow(string path)
    {
        void Throw() => throw new IllegalPathException(path);

        if (path.Split('/').Any(c => c.Any(invalidFileNameChars.Contains)))
        {
            Throw();
        }

        string result = Path.GetFullPath(path, basePath);

        if (!result.StartsWith(basePath, StringComparison.Ordinal))
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

    private string GetLockPath(string directoryPath)
    {
        string relativePath = NormalizePath(Path.GetRelativePath(basePath, directoryPath));

        int index = relativePath.IndexOf('/') + 1;
        if (index > 0)
        {
            return relativePath[..index];
        }

        return relativePath;
    }

    private Task<IDisposable> LockPath(string directoryPath, CancellationToken cancellationToken) => 
        lockService.LockPath(GetLockPath(directoryPath), cancellationToken);

    private async Task CreateDirectory(string directoryPath, CancellationToken cancellationToken)
    {
        using (await LockPath(directoryPath, cancellationToken))
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }
    }

    private async Task DeleteEmptyDirectories(string directoryPath, CancellationToken cancellationToken)
    {
        using (await LockPath(directoryPath, cancellationToken))
        {
            DirectoryInfo? directory = new(directoryPath);
            while
            (
                directory != null &&
                directory.FullName != basePath &&
                !directory.EnumerateFileSystemInfos().Any()
            )
            {
                directory.Delete(false);
                directory = directory.Parent;
            }
        }
    }
}