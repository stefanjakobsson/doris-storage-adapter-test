using DatasetFileUpload.Models;
using DatasetFileUpload.Models.BagIt;
using DatasetFileUpload.Services.Lock;
using DatasetFileUpload.Services.Storage;
using Nerdbank.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services;

public class FileService(
    IStorageService storageService,
    ILockService lockService)
{
    private readonly IStorageService storageService = storageService;
    private readonly ILockService lockService = lockService;

    private const string payloadManifestSha256FileName = "manifest-sha256.txt";
    private const string tagManifestSha256FileName = "tagmanifest-sha256.txt";
    private const string fetchFileName = "fetch.txt";
    private const string bagItFileName = "bagit.txt";
    private const string bagInfoFileName = "bag-info.txt";

    public async Task SetupVersion(DatasetVersionIdentifier datasetVersion)
    {
        if (!await lockService.TryLockDatasetVersion(datasetVersion))
        {
            throw new ConflictException();
        }

        try
        {
            await SetupVersionImpl(datasetVersion);
        }
        finally
        {
            await lockService.ReleaseDatasetVersionLock(datasetVersion);
        }
    }

    private async Task SetupVersionImpl(DatasetVersionIdentifier datasetVersion)
    {
        async Task CopyManifest(DatasetVersionIdentifier fromVersion, DatasetVersionIdentifier toVersion, bool payload)
        {
            var fileData = await storageService.GetFileData(GetManifestFilePath(fromVersion, payload));
            if (fileData != null)
            {
                await storageService.StoreFile(GetManifestFilePath(toVersion, payload), fileData.Stream);
            }
        }

        if (await storageService.ListFiles(GetDatasetVersionPath(datasetVersion))
                .GetAsyncEnumerator().MoveNextAsync())
        {
            // Files are already present for datasetVersion, abort
            return;
        }

        if (!TryGetPreviousVersionNumber(datasetVersion.VersionNumber, out string previousVersionNr))
        {
            return;
        }

        var previousVersion = new DatasetVersionIdentifier(datasetVersion.DatasetIdentifier, previousVersionNr);
        var fetch = await LoadFetch(previousVersion);
        var newFetch = new BagItFetch();

        await CopyManifest(previousVersion, datasetVersion, true);
        await CopyManifest(previousVersion, datasetVersion, false);

        string previousVersionUrl = "../" + UrlEncodePath(GetVersionPath(previousVersion)) + '/';

        foreach (var item in fetch.Items)
        {
            newFetch.AddOrUpdateItem(item);
        }

        await foreach (var file in ListFilesForDatasetVersion(previousVersion))
        {
            if (!fetch.Contains(file.Id))
            {
                newFetch.AddOrUpdateItem(new(file.Id, file.ContentSize, previousVersionUrl + UrlEncodePath(file.Id)));
            }
        }

        await StoreFetch(datasetVersion, newFetch);
    }

    public async Task PublishVersion(DatasetVersionIdentifier datasetVersion, bool openAccess, string doi)
    {
        if (!await lockService.TryLockDatasetVersion(datasetVersion))
        {
            throw new ConflictException();
        }

        try
        {
            await PublishVersionImpl(datasetVersion, openAccess, doi);
        }
        finally
        {
            await lockService.ReleaseDatasetVersionLock(datasetVersion);
        }
    }

    private async Task PublishVersionImpl(DatasetVersionIdentifier datasetVersion, bool openAccess, string doi)
    {
        async Task<byte[]> WriteTextToFile(string filePath, string text)
        {
            using var sha256 = SHA256.Create();
            using var hashStream = new CryptoStream(new MemoryStream(Encoding.UTF8.GetBytes(text)), sha256, CryptoStreamMode.Read);
            await storageService.StoreFile(GetFullFilePath(datasetVersion, filePath), hashStream);
            return sha256.Hash!;
        }

        async Task<byte[]?> GetChecksum(string filePath)
        {
            using var sha256 = SHA256.Create();
            var fileData = await storageService.GetFileData(GetFullFilePath(datasetVersion, filePath));

            if (fileData == null)
            {
                return null;
            }

            return sha256.ComputeHash(fileData.Stream);
        }

        // Kolla om redan publicerat, behövs anrop för att kolla om fil finns?

        var bagItChecksum = await WriteTextToFile(bagItFileName, 
            "BagIt-Version: 1.0\nTag-File-Character-Encoding: UTF-8");

        var bagInfoChecksum = await WriteTextToFile(bagInfoFileName, "Dummy: true");

        var payloadManifestChecksum = await GetChecksum(payloadManifestSha256FileName);

        // Add bagit.txt, bag-info.txt and manifest-sha256.txt to tagmanifest-sha256.txt
        var tagManifest = await LoadManifest(datasetVersion, false);
        tagManifest.AddOrUpdateItem(new(bagItFileName, bagItChecksum));
        tagManifest.AddOrUpdateItem(new(bagInfoFileName, bagInfoChecksum));
        if (payloadManifestChecksum != null)
        {
            tagManifest.AddOrUpdateItem(new(payloadManifestSha256FileName, payloadManifestChecksum));
        }
        await StoreManifest(datasetVersion, false, tagManifest);
    }

    public async Task<RoCrateFile> Upload(
        DatasetVersionIdentifier datasetVersion,
        FileType type,
        string filePath,
        Stream data)
    {
        filePath = GetFilePathOrThrow(type, filePath);

        if (!await lockService.TryLockFilePath(datasetVersion, filePath))
        {
            throw new ConflictException();
        }

        try
        {
            return await UploadImpl(datasetVersion, type, filePath, data);
        }
        finally
        {
            await lockService.ReleaseFilePathLock(datasetVersion, filePath);
        }
    }

    private async Task<RoCrateFile> UploadImpl(
        DatasetVersionIdentifier datasetVersion,
        FileType type,
        string filePath,
        Stream data)
    {
        async Task<string?> Deduplicate(byte[] checksum)
        {
            if (!TryGetPreviousVersionNumber(datasetVersion.VersionNumber, out var prevVersionNr))
            {
                return null;
            }

            var prevVersion = new DatasetVersionIdentifier(datasetVersion.DatasetIdentifier, prevVersionNr);
            var prevManifest = await LoadManifest(prevVersion, type == FileType.Data);
            var itemsWithEqualChecksum = prevManifest.GetItemsByChecksum(checksum);

            if (!itemsWithEqualChecksum.Any())
            {
                return null;
            }

            var prevFetch = await LoadFetch(prevVersion);

            // If we find an item with equal checksum in fetch.txt, use that URL
            foreach (var candidate in itemsWithEqualChecksum)
            {
                if (prevFetch.TryGetItem(candidate.FilePath, out var fetchItem))
                {
                    return fetchItem.Url;
                }
            }

            // Nothing found in fetch.txt, simply take first item's file path
            return "../" +
                UrlEncodePath(GetVersionPath(prevVersion)) + '/' +
                UrlEncodePath(itemsWithEqualChecksum.First().FilePath);
        }

        string fullFilePath = GetFullFilePath(datasetVersion, filePath);

        using var sha256 = SHA256.Create();
        var hashStream = new CryptoStream(data, sha256, CryptoStreamMode.Read);

        long bytesRead = 0;
        var monitoringStream = new MonitoringStream(hashStream);
        monitoringStream.DidRead += (_, e) =>
        {
            bytesRead += e.Count;
        };

        var result = await storageService.StoreFile(fullFilePath, monitoringStream);

        byte[] checksum = sha256.Hash!;

        string? url = await Deduplicate(checksum);
        if (url != null)
        {
            // Deduplication was successful, store in fetch and delete uploaded file
            await AddOrUpdateFetchItem(datasetVersion, new(filePath, bytesRead, url));
            await storageService.DeleteFile(fullFilePath);
        }
        else
        {
            // File is not a duplicate, remove from fetch if present there
            await RemoveItemFromFetch(datasetVersion, filePath);
        }

        // Update manifest
        await AddOrUpdateManifestItem(datasetVersion, new(filePath, checksum));

        //result.Id = fileName; ??
        result.Id = filePath;
        result.ContentSize = bytesRead;
        //result.EncodingFormat?
        result.Sha256 = Convert.ToHexString(checksum);
        // result.Url?

        return result;
    }

    public async Task Delete(
        DatasetVersionIdentifier datasetVersion,
        FileType type,
        string filePath)
    {
        filePath = GetFilePathOrThrow(type, filePath);

        if (!await lockService.TryLockFilePath(datasetVersion, filePath))
        {
            throw new ConflictException();
        }

        try
        {
           await DeleteImpl(datasetVersion, filePath);
        }
        finally
        {
            await lockService.ReleaseFilePathLock(datasetVersion, filePath);
        }
    }

    private async Task DeleteImpl(
        DatasetVersionIdentifier datasetVersion,
        string filePath)
    {
        await storageService.DeleteFile(GetFullFilePath(datasetVersion, filePath));
        await RemoveItemFromManifest(datasetVersion, filePath);
        await RemoveItemFromFetch(datasetVersion, filePath);
    }

    public async Task<StreamWithLength?> GetData(
        DatasetVersionIdentifier datasetVersion,
        FileType type,
        string filePath)
    {
        // Should we add some kind of locking here?
        // If so we need to distinguish between read and write locks (currently we only have write locks).
        // The requested file could potentially be added to fetch and removed from current version
        // after we found it in fetch and try to load it from current version, which will return
        // not found to the caller.

        filePath = GetFilePathOrThrow(type, filePath);

        var fetch = await LoadFetch(datasetVersion);

        if (fetch.TryGetItem(filePath, out var fetchItem))
        {
            filePath = GetDatasetPath(datasetVersion) + DecodeUrlEncodedPath(fetchItem.Url[2..]);
        }
        else
        {
            filePath = GetFullFilePath(datasetVersion, filePath);
        }

        return await storageService.GetFileData(filePath);
    }

    public async IAsyncEnumerable<RoCrateFile> ListFiles(DatasetVersionIdentifier datasetVersion)
    {
        // Should we add some kind of locking here?
        // If so we need to distinguish between read and write locks (currently we only have write locks).
        // Checksums and fetch can potentially be changed while processing this request,
        // leading to returning faulty checksums and other problems.

        var payloadChecksums = await LoadManifest(datasetVersion, true);
        var tagChecksums = await LoadManifest(datasetVersion, false);
        var fetch = await LoadFetch(datasetVersion);

        string? GetChecksum(string filePath)
        {
            var manifest = filePath.StartsWith("data/") ? payloadChecksums : tagChecksums;
            return manifest.TryGetItem(filePath, out var value) ? Convert.ToHexString(value.Checksum) : null;
        }

        await foreach (var file in ListFilesForDatasetVersion(datasetVersion))
        {
            file.Sha256 = GetChecksum(file.Id);

            yield return file;
        }

        foreach (var file in fetch.Items)
        {
            yield return new()
            {
                Id = file.FilePath,
                ContentSize = file.Length == null ? 0 : file.Length.Value,
                Sha256 = GetChecksum(file.FilePath)
            };
        }
    }


    private static string GetFilePathOrThrow(FileType type, string filePath)
    {
        foreach (string pathComponent in filePath.Split('/'))
        {
            if (pathComponent == "" ||
                pathComponent == "." ||
                pathComponent == "..")
            {
                throw new IllegalPathException(filePath);
            }
        }

        return type.ToString().ToLower() + '/' + filePath;
    }

    private static string GetDatasetPath(DatasetVersionIdentifier datasetVersion) =>
        datasetVersion.DatasetIdentifier;

    private static string GetVersionPath(DatasetVersionIdentifier datasetVersion) =>
        datasetVersion.DatasetIdentifier + '-' + datasetVersion.VersionNumber;

    private static string GetDatasetVersionPath(DatasetVersionIdentifier datasetVersion) =>
        GetDatasetPath(datasetVersion) + '/' + GetVersionPath(datasetVersion);

    private static string GetFullFilePath(DatasetVersionIdentifier datasetVersion, string filePath) =>
        GetDatasetVersionPath(datasetVersion) + '/' + filePath;

    private static string GetManifestFileName(bool payload) =>
        payload ? payloadManifestSha256FileName : tagManifestSha256FileName;

    private static string GetManifestFilePath(DatasetVersionIdentifier datasetVersion, bool payload) =>
        GetFullFilePath(datasetVersion, GetManifestFileName(payload));

    private static string GetFetchFilePath(DatasetVersionIdentifier datasetVersion) =>
        GetFullFilePath(datasetVersion, fetchFileName);

    private static string UrlEncodePath(string path) =>
        string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    private static string DecodeUrlEncodedPath(string path) =>
        string.Join('/', path.Split('/').Select(Uri.UnescapeDataString));

    private async Task<BagItManifest> LoadManifest(DatasetVersionIdentifier datasetVersion, bool payloadManifest)
    {
        var fileData = await storageService.GetFileData(GetManifestFilePath(datasetVersion, payloadManifest));

        if (fileData == null)
        {
            return new();
        }

        return await BagItManifest.Parse(fileData.Stream);
    }

    private async Task<BagItFetch> LoadFetch(DatasetVersionIdentifier datasetVersion)
    {
        var fileData = await storageService.GetFileData(GetFetchFilePath(datasetVersion));

        if (fileData == null)
        {
            return new();
        }

        return await BagItFetch.Parse(fileData.Stream);
    }

    private Task AddOrUpdateManifestItem(DatasetVersionIdentifier datasetVersion, BagItManifestItem item) =>
        LockAndUpdateManifest(datasetVersion, item.FilePath.StartsWith("data/"), manifest => manifest.AddOrUpdateItem(item));

    private Task AddOrUpdateFetchItem(DatasetVersionIdentifier datasetVersion, BagItFetchItem item) =>
        LockAndUpdateFetch(datasetVersion, fetch => fetch.AddOrUpdateItem(item));

    private Task RemoveItemFromManifest(DatasetVersionIdentifier datasetVersion, string filePath) =>
        LockAndUpdateManifest(datasetVersion, filePath.StartsWith("data/"), manifest => manifest.RemoveItem(filePath));

    private Task RemoveItemFromFetch(DatasetVersionIdentifier datasetVersion, string filePath) =>
        LockAndUpdateFetch(datasetVersion, fetch => fetch.RemoveItem(filePath));

    private async Task LockAndUpdateManifest(DatasetVersionIdentifier datasetVersion, bool payload, Func<BagItManifest, bool> action)
    {
        // This method assumes that datasetVersion is not locked for writing,
        // i.e. that it is called "within" a file lock.

        string manifestFilePath = GetManifestFileName(payload);
        await lockService.LockFilePath(datasetVersion, manifestFilePath);

        try
        {
            var manifest = await LoadManifest(datasetVersion, payload);

            if (action(manifest))
            {
                await StoreManifest(datasetVersion, payload, manifest);
            }
        }
        finally
        {
            await lockService.ReleaseFilePathLock(datasetVersion, manifestFilePath);
        }
    }

    private async Task LockAndUpdateFetch(DatasetVersionIdentifier datasetVersion, Func<BagItFetch, bool> action)
    {
        // This method assumes that datasetVersion is not locked for writing,
        // i.e. that it is called "within" a file lock.

        await lockService.LockFilePath(datasetVersion, fetchFileName);

        try
        {
            var fetch = await LoadFetch(datasetVersion);

            if (action(fetch))
            {
                await StoreFetch(datasetVersion, fetch);
            }
        }
        finally
        {
            await lockService.ReleaseFilePathLock(datasetVersion, fetchFileName);
        }
    }

    private Task StoreManifest(DatasetVersionIdentifier datasetVersion, bool payload, BagItManifest manifest)
    {
        string filePath = GetManifestFilePath(datasetVersion, payload);

        if (manifest.Items.Any())
        {
            return storageService.StoreFile(filePath, new MemoryStream(manifest.Serialize()));
        }

        return storageService.DeleteFile(filePath);
    }

    private Task StoreFetch(DatasetVersionIdentifier datasetVersion, BagItFetch fetch)
    {
        string filePath = GetFetchFilePath(datasetVersion);

        if (fetch.Items.Any())
        {
            return storageService.StoreFile(filePath, new MemoryStream(fetch.Serialize()));
        }

        return storageService.DeleteFile(filePath);
    }

    private async IAsyncEnumerable<RoCrateFile> ListFilesForDatasetVersion(DatasetVersionIdentifier datasetVersion)
    {
        string path = GetDatasetVersionPath(datasetVersion);

        await foreach (var file in storageService.ListFiles(path))
        {
            file.Id = file.Id[(path.Length + 1)..];

            if (file.Id.StartsWith("data/") ||
                file.Id.StartsWith("documentation/"))
            {
                yield return file;
            }
        }
    }

    private static bool TryGetPreviousVersionNumber(string versionNumber, out string previousVersionNumber)
    {
        var values = versionNumber.Split('.');
        int versionMajor = int.Parse(values[0]);

        if (versionMajor > 1)
        {
            previousVersionNumber = (versionMajor - 1).ToString();
            return true;
        }

        previousVersionNumber = "";
        return false;
    }
}
