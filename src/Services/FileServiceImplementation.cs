using DatasetFileUpload.Models;
using DatasetFileUpload.Models.BagIt;
using DatasetFileUpload.Services.Storage;
using Nerdbank.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services;

public class FileServiceImplementation(IStorageService storageService)
{
    private readonly IStorageService storageService = storageService;

    private const string payloadManifestSha256FileName = "manifest-sha256.txt";
    private const string tagManifestSha256FileName = "tagmanifest-sha256.txt";
    private const string fetchFileName = "fetch.txt";

    public async Task SetupVersion(DatasetVersionIdentifier datasetVersion)
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

    public async Task<RoCrateFile> Upload(
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

        filePath = GetFilePathOrThrow(type, filePath);
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

        await storageService.DeleteFile(GetFullFilePath(datasetVersion, filePath));
        await RemoveItemFromManifest(datasetVersion, filePath);
        await RemoveItemFromFetch(datasetVersion, filePath);
    }

    public async Task<StreamWithLength?> GetData(
        DatasetVersionIdentifier datasetVersion,
        FileType type,
        string filePath)
    {
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

    private static string GetManifestFilePath(DatasetVersionIdentifier datasetVersion, bool payload) =>
        GetFullFilePath(datasetVersion, payload ? payloadManifestSha256FileName : tagManifestSha256FileName);

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

    private async Task AddOrUpdateManifestItem(DatasetVersionIdentifier datasetVersion, BagItManifestItem item)
    {
        bool payload = item.FilePath.StartsWith("data/");
        var manifest = await LoadManifest(datasetVersion, payload);

        if (manifest.AddOrUpdateItem(item))
        {
            await StoreManifest(datasetVersion, payload, manifest);
        }
    }

    private async Task AddOrUpdateFetchItem(DatasetVersionIdentifier datasetVersion, BagItFetchItem item)
    {
        var fetch = await LoadFetch(datasetVersion);

        if (fetch.AddOrUpdateItem(item))
        {
            await StoreFetch(datasetVersion, fetch);
        }
    }

    private async Task RemoveItemFromManifest(DatasetVersionIdentifier datasetVersion, string filePath)
    {
        bool payloadManifest = filePath.StartsWith("data/");
        var manifest = await LoadManifest(datasetVersion, payloadManifest);

        if (manifest.RemoveItem(filePath))
        {
            await StoreManifest(datasetVersion, payloadManifest, manifest);
        }
    }

    private async Task RemoveItemFromFetch(DatasetVersionIdentifier datasetVersion, string filePath)
    {
        var fetch = await LoadFetch(datasetVersion);

        if (fetch.RemoveItem(filePath))
        {
            await StoreFetch(datasetVersion, fetch);
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
