using ByteSizeLib;
using DatasetFileUpload.Models;
using DatasetFileUpload.Models.BagIt;
using DatasetFileUpload.Services.Exceptions;
using DatasetFileUpload.Services.Lock;
using DatasetFileUpload.Services.Storage;
using Nerdbank.Streams;
using System;
using System.Collections.Generic;
using System.Globalization;
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
            await ThrowIfPublished(datasetVersion);
            await SetupVersionImpl(datasetVersion);
        }
        finally
        {
            await lockService.ReleaseDatasetVersionLock(datasetVersion);
        }
    }

    private async Task SetupVersionImpl(DatasetVersionIdentifier datasetVersion)
    {
        async Task CopyPayloadManifest(DatasetVersionIdentifier fromVersion, DatasetVersionIdentifier toVersion)
        {
            using var fileData = await storageService.GetFileData(GetManifestFilePath(fromVersion, true));
            if (fileData != null)
            {
                await storageService.StoreFile(GetManifestFilePath(toVersion, true), fileData);
            }
        }

        async Task CopyTagManifest(DatasetVersionIdentifier fromVersion, DatasetVersionIdentifier toVersion)
        {
            using var fileData = await storageService.GetFileData(GetManifestFilePath(fromVersion, false));
            if (fileData != null)
            {
                var sourceManifest = await BagItManifest.Parse(fileData.Stream);
                var newManifest = new BagItManifest();

                foreach (var item in sourceManifest.Items.Where(i => IsDocumentationFile(i.FilePath)))
                {
                    newManifest.AddOrUpdateItem(item);
                }

                await StoreManifest(toVersion, false, newManifest);
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

        await CopyPayloadManifest(previousVersion, datasetVersion);
        await CopyTagManifest(previousVersion, datasetVersion);

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

    public async Task PublishVersion(DatasetVersionIdentifier datasetVersion, AccessRightEnum accessRight, string doi)
    {
        if (!await lockService.TryLockDatasetVersion(datasetVersion))
        {
            throw new ConflictException();
        }

        try
        {
            await PublishVersionImpl(datasetVersion, accessRight, doi);
        }
        finally
        {
            await lockService.ReleaseDatasetVersionLock(datasetVersion);
        }
    }

    private async Task PublishVersionImpl(DatasetVersionIdentifier datasetVersion, AccessRightEnum accessRight, string doi)
    {
        Task WriteBytes(string filePath, byte[] data) =>
            storageService.StoreFile(GetFullFilePath(datasetVersion, filePath), CreateStreamFromByteArray(data));

        async Task<(T, byte[] Checksum)?> LoadWithChecksum<T>(string filePath, Func<Stream, Task<T>> func)
        {
            using var fileData = await storageService.GetFileData(filePath);

            if (fileData == null)
            {
                return null;
            }

            using var sha256 = SHA256.Create();
            using var hashStream = new CryptoStream(fileData.Stream, sha256, CryptoStreamMode.Read);

            return (await func(hashStream), sha256.Hash!);
        }

        Task<(BagItManifest Manifest, byte[] Checksum)?> LoadManifestWithChecksum() =>
            LoadWithChecksum(GetManifestFilePath(datasetVersion, true), BagItManifest.Parse);

        Task<(BagItFetch Fetch, byte[] Checksum)?> LoadFetchWithChecksum() =>
            LoadWithChecksum(GetFetchFilePath(datasetVersion), BagItFetch.Parse);

        var fetch = await LoadFetchWithChecksum();
        long octetCount = 0;
        long totalSize = 0;
        await foreach (var file in ListFilesForDatasetVersion(datasetVersion))
        {
            totalSize += file.ContentSize;
            if (IsDataFile(file.Id))
            {
                octetCount += file.ContentSize;
            }
        }
        foreach (var item in fetch?.Fetch?.Items ?? [])
        {
            if (item.Length != null)
            {
                totalSize += item.Length.Value;

                if (IsDataFile(item.FilePath))
                {
                    octetCount += item.Length.Value;
                }
            }
        }

        var payloadManifest = await LoadManifestWithChecksum();

        var bagInfo = new BagItInfo
        {
            ExternalIdentifier = doi,
            BagGroupIdentifier = datasetVersion.DatasetIdentifier,
            BaggingDate = DateTime.UtcNow,
            BagSize = ByteSize.FromBytes(totalSize).ToBinaryString(CultureInfo.InvariantCulture),
            PayloadOxum = new(octetCount, payloadManifest?.Manifest?.Items?.LongCount() ?? 0),
            AccessRight = accessRight,
            DatasetStatus = DatasetStatusEnum.completed
        };
        byte[] bagInfoContents = bagInfo.Serialize();

        byte[] bagItContents = Encoding.UTF8.GetBytes("BagIt-Version: 1.0\nTag-File-Character-Encoding: UTF-8");

        // Add bagit.txt, bag-info.txt and manifest-sha256.txt to tagmanifest-sha256.txt
        var tagManifest = await LoadManifest(datasetVersion, false);
        tagManifest.AddOrUpdateItem(new(bagItFileName, SHA256.HashData(bagItContents)));
        tagManifest.AddOrUpdateItem(new(bagInfoFileName, SHA256.HashData(bagInfoContents)));
        if (payloadManifest != null)
        {
            tagManifest.AddOrUpdateItem(new(payloadManifestSha256FileName, payloadManifest.Value.Checksum));
        }
        if (fetch != null)
        {
            tagManifest.AddOrUpdateItem(new(fetchFileName, fetch.Value.Checksum));
        }
        await StoreManifest(datasetVersion, false, tagManifest);

        await WriteBytes(bagInfoFileName, bagInfoContents);
        await WriteBytes(bagItFileName, bagItContents);
    }

    public async Task WithdrawVersion(DatasetVersionIdentifier datasetVersion)
    {
        if (!await lockService.TryLockDatasetVersion(datasetVersion))
        {
            throw new ConflictException();
        }

        try
        {
            if (!await VersionIsPublished(datasetVersion))
            {
                throw new DatasetStatusException();
            }

            await WithdrawVersionImpl(datasetVersion);
        }
        finally
        {
            await lockService.ReleaseDatasetVersionLock(datasetVersion);
        }
    }

    private async Task WithdrawVersionImpl(DatasetVersionIdentifier datasetVersion)
    {
        async Task<BagItInfo?> LoadBagInfo()
        {
            using var data = await storageService.GetFileData(GetFullFilePath(datasetVersion, bagInfoFileName));

            if (data == null)
            {
                return null;
            }

            return await BagItInfo.Parse(data.Stream);
        }

        var bagInfo = await LoadBagInfo();

        if (bagInfo == null)
        {
            // Do we need to throw an exception here?
            return;
        }

        bagInfo.DatasetStatus = DatasetStatusEnum.withdrawn;

        var bagInfoContents = bagInfo.Serialize();

        var tagManifest = await LoadManifest(datasetVersion, false);
        tagManifest.AddOrUpdateItem(new(bagInfoFileName, SHA256.HashData(bagInfoContents)));
        await StoreManifest(datasetVersion, false, tagManifest);

        await storageService.StoreFile(GetFullFilePath(datasetVersion, bagInfoFileName),
            CreateStreamFromByteArray(bagInfoContents));
    }

    public async Task<RoCrateFile> Upload(
        DatasetVersionIdentifier datasetVersion,
        FileTypeEnum type,
        string filePath,
        StreamWithLength data)
    {
        filePath = GetFilePathOrThrow(type, filePath);

        if (!await lockService.TryLockFilePath(datasetVersion, filePath))
        {
            throw new ConflictException();
        }

        try
        {
            await ThrowIfPublished(datasetVersion);
            return await UploadImpl(datasetVersion, type, filePath, data);
        }
        finally
        {
            await lockService.ReleaseFilePathLock(datasetVersion, filePath);
        }
    }

    private async Task<RoCrateFile> UploadImpl(
        DatasetVersionIdentifier datasetVersion,
        FileTypeEnum type,
        string filePath,
        StreamWithLength data)
    {
        async Task<string?> Deduplicate(byte[] checksum)
        {
            if (!TryGetPreviousVersionNumber(datasetVersion.VersionNumber, out var prevVersionNr))
            {
                return null;
            }

            var prevVersion = new DatasetVersionIdentifier(datasetVersion.DatasetIdentifier, prevVersionNr);
            var prevManifest = await LoadManifest(prevVersion, type == FileTypeEnum.Data);
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
        var hashStream = new CryptoStream(data.Stream, sha256, CryptoStreamMode.Read);

        long bytesRead = 0;
        var monitoringStream = new MonitoringStream(hashStream);
        monitoringStream.DidRead += (_, e) =>
        {
            bytesRead += e.Count;
        };

        var result = await storageService.StoreFile(fullFilePath, new(monitoringStream, data.Length));

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

        result.Id = filePath;
        result.ContentSize = bytesRead;
        result.Sha256 = Convert.ToHexString(checksum);

        return result;
    }

    public async Task Delete(
        DatasetVersionIdentifier datasetVersion,
        FileTypeEnum type,
        string filePath)
    {
        filePath = GetFilePathOrThrow(type, filePath);

        if (!await lockService.TryLockFilePath(datasetVersion, filePath))
        {
            throw new ConflictException();
        }

        try
        {
            await ThrowIfPublished(datasetVersion);
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
        FileTypeEnum type,
        string filePath)
    {
        // Should we add some kind of locking here?
        // If so we need to distinguish between read and write locks (currently we only have write locks).
        // The requested file could potentially be added to fetch and removed from current version
        // after we found it in fetch and try to load it from current version, which will return
        // not found to the caller.

        filePath = GetFilePathOrThrow(type, filePath);
        var fetch = await LoadFetch(datasetVersion);
        filePath = GetActualFilePath(datasetVersion, fetch, filePath);

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
            var manifest = IsDataFile(filePath) ? payloadChecksums : tagChecksums;
            return manifest.TryGetItem(filePath, out var value) ? Convert.ToHexString(value.Checksum) : null;
        }

        await foreach (var file in ListFilesForDatasetVersion(datasetVersion))
        {
            file.Sha256 = GetChecksum(file.Id);
            file.Id = StripFileTypePrefix(file.Id);

            yield return file;
        }

        foreach (var file in fetch.Items)
        {
            yield return new()
            {
                Id = StripFileTypePrefix(file.FilePath),
                ContentSize = file.Length == null ? 0 : file.Length.Value,
                Sha256 = GetChecksum(file.FilePath)
            };
        }
    }

    // Change return type to class/record? Reuse return type from ListFiles?
    public async IAsyncEnumerable<(FileTypeEnum Type, string FilePath, StreamWithLength Data)> GetDataByPaths(
        DatasetVersionIdentifier datasetVersion, string[] paths)
    {
        var payloadManifest = await LoadManifest(datasetVersion, true);
        var tagManifest = await LoadManifest(datasetVersion, false);
        var fetch = await LoadFetch(datasetVersion);

        foreach (var filePath in
            payloadManifest.Items.Concat(tagManifest.Items).Select(i => i.FilePath))
        {
            if (paths.Length > 0 && !paths.Any(filePath.StartsWith))
            {
                continue;
            }

            string actualFilePath = GetActualFilePath(datasetVersion, fetch, filePath);
            var data = await storageService.GetFileData(actualFilePath);

            if (data != null && TryGetFileType(filePath, out var type))
            {
                yield return (type, StripFileTypePrefix(filePath), data);
            }
        }

        /*await foreach (var file in ListFilesForDatasetVersion(datasetVersion))
        {
            if (paths.Length > 0 && !paths.Any(file.Id.StartsWith))
            {
                continue;
            }

            string filePath;

            if (fetch.TryGetItem(file.Id, out var fetchItem))
            {
                filePath = GetDatasetPath(datasetVersion) + DecodeUrlEncodedPath(fetchItem.Url[2..]);
            }
            else
            {
                filePath = GetFullFilePath(datasetVersion, file.Id);
            }

            var data = await storageService.GetFileData(filePath);

            if (data != null)
            {
                yield return (file.AdditionalType, StripTypePrefix(file.Id), data.Stream);
            }
        }*/
    }


    private static string GetFilePathOrThrow(FileTypeEnum type, string filePath)
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

    private static bool TryGetFileType(string filePath, out FileTypeEnum type)
    {
        if (filePath.StartsWith("data/"))
        {
            type = FileTypeEnum.Data;
            return true;
        }

        if (filePath.StartsWith("documentation/"))
        {
            type = FileTypeEnum.Documentation;
            return true;
        }

        type = default;
        return false;
    }

    private static bool IsDataFile(string filePath) => TryGetFileType(filePath, out var type) && type == FileTypeEnum.Data;

    private static bool IsDocumentationFile(string filePath) => TryGetFileType(filePath, out var type) && type == FileTypeEnum.Documentation;

    private static string StripFileTypePrefix(string filePath)
    {
        if (TryGetFileType(filePath, out var type))
        {
            return type switch
            {
                FileTypeEnum.Data => filePath["data/".Length..],
                FileTypeEnum.Documentation => filePath["documentation/".Length..],
                _ => filePath
            };
        };

        return filePath;
    }

    private static string GetFetchFilePath(DatasetVersionIdentifier datasetVersion) =>
        GetFullFilePath(datasetVersion, fetchFileName);

    private static string UrlEncodePath(string path) =>
        string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    private static string DecodeUrlEncodedPath(string path) =>
        string.Join('/', path.Split('/').Select(Uri.UnescapeDataString));

    private async Task<BagItManifest> LoadManifest(DatasetVersionIdentifier datasetVersion, bool payloadManifest)
    {
        using var fileData = await storageService.GetFileData(GetManifestFilePath(datasetVersion, payloadManifest));

        if (fileData == null)
        {
            return new();
        }

        return await BagItManifest.Parse(fileData.Stream);
    }

    private async Task<BagItFetch> LoadFetch(DatasetVersionIdentifier datasetVersion)
    {
        using var fileData = await storageService.GetFileData(GetFetchFilePath(datasetVersion));

        if (fileData == null)
        {
            return new();
        }

        return await BagItFetch.Parse(fileData.Stream);
    }

    private Task AddOrUpdateManifestItem(DatasetVersionIdentifier datasetVersion, BagItManifestItem item) =>
        LockAndUpdateManifest(datasetVersion, IsDataFile(item.FilePath), manifest => manifest.AddOrUpdateItem(item));

    private Task AddOrUpdateFetchItem(DatasetVersionIdentifier datasetVersion, BagItFetchItem item) =>
        LockAndUpdateFetch(datasetVersion, fetch => fetch.AddOrUpdateItem(item));

    private Task RemoveItemFromManifest(DatasetVersionIdentifier datasetVersion, string filePath) =>
        LockAndUpdateManifest(datasetVersion, IsDataFile(filePath), manifest => manifest.RemoveItem(filePath));

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
            return storageService.StoreFile(filePath, CreateStreamFromByteArray(manifest.Serialize()));
        }

        return storageService.DeleteFile(filePath);
    }

    private Task StoreFetch(DatasetVersionIdentifier datasetVersion, BagItFetch fetch)
    {
        string filePath = GetFetchFilePath(datasetVersion);

        if (fetch.Items.Any())
        {
            return storageService.StoreFile(filePath, CreateStreamFromByteArray(fetch.Serialize()));
        }

        return storageService.DeleteFile(filePath);
    }

    private static string GetActualFilePath(DatasetVersionIdentifier datasetVersion, BagItFetch fetch, string filePath)
    {
        if (fetch.TryGetItem(filePath, out var fetchItem))
        {
            return GetDatasetPath(datasetVersion) + DecodeUrlEncodedPath(fetchItem.Url[2..]);
        }

        return GetFullFilePath(datasetVersion, filePath);
    }

    private async IAsyncEnumerable<RoCrateFile> ListFilesForDatasetVersion(DatasetVersionIdentifier datasetVersion)
    {
        string path = GetDatasetVersionPath(datasetVersion);

        await foreach (var file in storageService.ListFiles(path))
        {
            file.Id = file.Id[(path.Length + 1)..];

            if (TryGetFileType(file.Id, out var fileType))
            {
                file.AdditionalType = fileType;
                yield return file;
            }
        }
    }

    private static StreamWithLength CreateStreamFromByteArray(byte[] data) => new(new MemoryStream(data), data.LongLength);

    private async Task<bool> VersionIsPublished(DatasetVersionIdentifier datasetVersion)
    {
        using var data = await storageService.GetFileData(GetFullFilePath(datasetVersion, bagItFileName));

        return data != null;
    }

    private async Task ThrowIfPublished(DatasetVersionIdentifier datasetVersion)
    {
        if (await VersionIsPublished(datasetVersion))
        {
            throw new DatasetStatusException();
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
