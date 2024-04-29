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

public class ServiceImplementation(
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

    public async Task SetupDatasetVersion(DatasetVersionIdentifier datasetVersion)
    {
        if (!await lockService.TryLockDatasetVersion(datasetVersion))
        {
            throw new ConflictException();
        }

        try
        {
            await ThrowIfHasBeenPublished(datasetVersion);
            await SetupDatasetVersionImpl(datasetVersion);
        }
        finally
        {
            await lockService.ReleaseDatasetVersionLock(datasetVersion);
        }
    }

    private async Task SetupDatasetVersionImpl(DatasetVersionIdentifier datasetVersion)
    {
        async Task CopyPayloadManifest(DatasetVersionIdentifier fromVersion, DatasetVersionIdentifier toVersion)
        {
            using var fileData = await storageService.GetFileData(GetManifestFilePath(fromVersion, true));
            if (fileData != null)
            {
                await storageService.StoreFile(GetManifestFilePath(toVersion, true), fileData, "text/plain");
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

        string previousVersionUrl = "../" + UrlEncodePath(GetVersionPath(previousVersion)) + '/';

        foreach (var item in fetch.Items)
        {
            newFetch.AddOrUpdateItem(item);
        }

        await foreach (var file in ListPayloadFiles(previousVersion))
        {
            if (!fetch.Contains(file.Path))
            {
                newFetch.AddOrUpdateItem(new(file.Path, file.Size, previousVersionUrl + UrlEncodePath(file.Path)));
            }
        }

        await StoreFetch(datasetVersion, newFetch);
    }

    public async Task PublishDatasetVersion(DatasetVersionIdentifier datasetVersion, AccessRightEnum accessRight, string doi)
    {
        if (!await lockService.TryLockDatasetVersion(datasetVersion))
        {
            throw new ConflictException();
        }

        try
        {
            await PublishDatasetVersionImpl(datasetVersion, accessRight, doi);
        }
        finally
        {
            await lockService.ReleaseDatasetVersionLock(datasetVersion);
        }
    }

    private async Task PublishDatasetVersionImpl(DatasetVersionIdentifier datasetVersion, AccessRightEnum accessRight, string doi)
    {
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
        await foreach (var file in ListPayloadFiles(datasetVersion))
        {
            octetCount += file.Size;
        }
        foreach (var item in fetch?.Fetch?.Items ?? [])
        {
            if (item.Length != null)
            {
                octetCount += item.Length.Value;
            }
        }

        var payloadManifest = await LoadManifestWithChecksum();

        var bagInfo = new BagItInfo
        {
            ExternalIdentifier = doi,
            BagGroupIdentifier = datasetVersion.DatasetIdentifier,
            BaggingDate = DateTime.UtcNow,
            BagSize = ByteSize.FromBytes(octetCount).ToBinaryString(CultureInfo.InvariantCulture),
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
        await StoreBagInfo(datasetVersion, bagInfoContents);
        await storageService.StoreFile(GetBagItFilePath(datasetVersion), CreateStreamFromByteArray(bagItContents), "text/plain");
    }

    public async Task WithdrawDatasetVersion(DatasetVersionIdentifier datasetVersion)
    {
        if (!await lockService.TryLockDatasetVersion(datasetVersion))
        {
            throw new ConflictException();
        }

        try
        {
            if (!await VersionHasBeenPublished(datasetVersion))
            {
                throw new DatasetStatusException();
            }

            await WithdrawDatasetVersionImpl(datasetVersion);
        }
        finally
        {
            await lockService.ReleaseDatasetVersionLock(datasetVersion);
        }
    }

    private async Task WithdrawDatasetVersionImpl(DatasetVersionIdentifier datasetVersion)
    {
        var bagInfo = await LoadBagInfo(datasetVersion);

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

        await StoreBagInfo(datasetVersion, bagInfoContents);
    }

    public async Task<RoCrateFile> StoreFile(
        DatasetVersionIdentifier datasetVersion,
        FileTypeEnum type,
        string filePath,
        StreamWithLength data,
        string? contentType)
    {
        filePath = GetFilePathOrThrow(type, filePath);

        if (!await lockService.TryLockFilePath(datasetVersion, filePath))
        {
            throw new ConflictException();
        }

        try
        {
            await ThrowIfHasBeenPublished(datasetVersion);
            return await StoreFileImpl(datasetVersion, type, filePath, data, contentType);
        }
        finally
        {
            await lockService.ReleaseFilePathLock(datasetVersion, filePath);
        }
    }

    private async Task<RoCrateFile> StoreFileImpl(
        DatasetVersionIdentifier datasetVersion,
        FileTypeEnum type,
        string filePath,
        StreamWithLength data,
        string? contentType)
    {
        async Task<string?> Deduplicate(byte[] checksum)
        {
            if (!TryGetPreviousVersionNumber(datasetVersion.VersionNumber, out var prevVersionNr))
            {
                return null;
            }

            var prevVersion = new DatasetVersionIdentifier(datasetVersion.DatasetIdentifier, prevVersionNr);
            var prevManifest = await LoadManifest(prevVersion, true);
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

        var result = await storageService.StoreFile(fullFilePath, data with { Stream = monitoringStream }, contentType);

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

        // Update payload manifest
        await AddOrUpdatePayloadManifestItem(datasetVersion, new(filePath, checksum));

        return new()
        {
            Id = result.Path,
            AdditionalType = type,
            ContentSize = bytesRead,
            DateCreated = result.DateCreated,
            DateModified = result.DateModified,
            EncodingFormat = result.ContentType,
            Sha256 = Convert.ToHexString(checksum),
            Url = null
        };
    }

    public async Task DeleteFile(
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
            await ThrowIfHasBeenPublished(datasetVersion);
            await DeleteFileImpl(datasetVersion, filePath);
        }
        finally
        {
            await lockService.ReleaseFilePathLock(datasetVersion, filePath);
        }
    }

    private async Task DeleteFileImpl(
        DatasetVersionIdentifier datasetVersion,
        string filePath)
    {
        await storageService.DeleteFile(GetFullFilePath(datasetVersion, filePath));
        await RemoveItemFromPayloadManifest(datasetVersion, filePath);
        await RemoveItemFromFetch(datasetVersion, filePath);
    }

    public async Task<StreamWithLength?> GetFileData(
        DatasetVersionIdentifier datasetVersion,
        FileTypeEnum type,
        string filePath,
        bool restrictToPubliclyAccessible)
    {
        // Should we add some kind of locking here?
        // If so we need to distinguish between read and write locks (currently we only have write locks).
        // The requested file could potentially be added to fetch and removed from current version
        // after we found it in fetch and try to load it from current version, which will return
        // not found to the caller.

        filePath = GetFilePathOrThrow(type, filePath);

        if (restrictToPubliclyAccessible)
        {
            // Do not return file data unless dataset version is published (and not withdrawn),
            // and file type is either documentation (which entails publically accessible) or
            // access right is public.

            var bagInfo = await LoadBagInfo(datasetVersion);

            if (bagInfo == null)
            {
                return null;
            }

            if (bagInfo.DatasetStatus != DatasetStatusEnum.completed ||
                type == FileTypeEnum.data && bagInfo.AccessRight != AccessRightEnum.@public)
            {
                return null;
            }
        }

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

        var payloadManifest = await LoadManifest(datasetVersion, true);
        var fetch = await LoadFetch(datasetVersion);

        string? GetChecksum(string filePath) =>
            payloadManifest.TryGetItem(filePath, out var value) ? Convert.ToHexString(value.Checksum) : null;

        await foreach (var file in ListPayloadFiles(datasetVersion))
        {
            TryGetFileType(file.Path, out var type);

            yield return new()
            {
                Id = StripFileTypePrefix(file.Path),
                AdditionalType = type,
                ContentSize = file.Size,
                DateCreated = file.DateCreated,
                DateModified = file.DateModified,
                EncodingFormat = file.ContentType,
                Sha256 = GetChecksum(file.Path),
                Url = null
            };
        }

        foreach (var file in fetch.Items)
        {
            yield return new()
            {
                Id = StripFileTypePrefix(file.FilePath),
                AdditionalType = TryGetFileType(file.FilePath, out var type) ? type : default,
                ContentSize = file.Length == null ? 0 : file.Length.Value,
                Sha256 = GetChecksum(file.FilePath)
            };
        }
    }

    // Change return type to class/record? Reuse return type from ListFiles?
    public async IAsyncEnumerable<(FileTypeEnum Type, string FilePath, StreamWithLength Data)> GetFileDataByPaths(
        DatasetVersionIdentifier datasetVersion, string[] paths)
    {
        var payloadManifest = await LoadManifest(datasetVersion, true);
        var fetch = await LoadFetch(datasetVersion);

        foreach (var filePath in payloadManifest.Items.Select(i => i.FilePath))
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

        return "data/" + type.ToString() + '/' + filePath;
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

    private static string GetBagInfoFilePath(DatasetVersionIdentifier datasetVersion) =>
        GetFullFilePath(datasetVersion, bagInfoFileName);

    private static string GetBagItFilePath(DatasetVersionIdentifier datasetVersion) =>
        GetFullFilePath(datasetVersion, bagItFileName);

    private static bool TryGetFileType(string filePath, out FileTypeEnum type)
    {
        if (filePath.StartsWith("data/data/"))
        {
            type = FileTypeEnum.data;
            return true;
        }

        if (filePath.StartsWith("data/documentation/"))
        {
            type = FileTypeEnum.documentation;
            return true;
        }

        type = default;
        return false;
    }

    private static string StripFileTypePrefix(string filePath)
    {
        if (TryGetFileType(filePath, out var type))
        {
            return type switch
            {
                FileTypeEnum.data => filePath["data/data/".Length..],
                FileTypeEnum.documentation => filePath["data/documentation/".Length..],
                _ => filePath
            };
        };

        return filePath;
    }

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

    private async Task<BagItInfo?> LoadBagInfo(DatasetVersionIdentifier datasetVersion)
    {
        using var data = await storageService.GetFileData(GetBagInfoFilePath(datasetVersion));

        if (data == null)
        {
            return null;
        }

        return await BagItInfo.Parse(data.Stream);
    }

    private Task AddOrUpdatePayloadManifestItem(DatasetVersionIdentifier datasetVersion, BagItManifestItem item) =>
        LockAndUpdatePayloadManifest(datasetVersion, manifest => manifest.AddOrUpdateItem(item));

    private Task AddOrUpdateFetchItem(DatasetVersionIdentifier datasetVersion, BagItFetchItem item) =>
        LockAndUpdateFetch(datasetVersion, fetch => fetch.AddOrUpdateItem(item));

    private Task RemoveItemFromPayloadManifest(DatasetVersionIdentifier datasetVersion, string filePath) =>
        LockAndUpdatePayloadManifest(datasetVersion, manifest => manifest.RemoveItem(filePath));

    private Task RemoveItemFromFetch(DatasetVersionIdentifier datasetVersion, string filePath) =>
        LockAndUpdateFetch(datasetVersion, fetch => fetch.RemoveItem(filePath));

    private async Task LockAndUpdatePayloadManifest(DatasetVersionIdentifier datasetVersion, Func<BagItManifest, bool> action)
    {
        // This method assumes that datasetVersion is not locked for writing,
        // i.e. that it is called "within" a file lock.

        string manifestFilePath = GetManifestFileName(true);
        await lockService.LockFilePath(datasetVersion, manifestFilePath);

        try
        {
            var manifest = await LoadManifest(datasetVersion, true);

            if (action(manifest))
            {
                await StoreManifest(datasetVersion, true, manifest);
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
            return storageService.StoreFile(filePath, CreateStreamFromByteArray(manifest.Serialize()), "text/plain");
        }

        return storageService.DeleteFile(filePath);
    }

    private Task StoreFetch(DatasetVersionIdentifier datasetVersion, BagItFetch fetch)
    {
        string filePath = GetFetchFilePath(datasetVersion);

        if (fetch.Items.Any())
        {
            return storageService.StoreFile(filePath, CreateStreamFromByteArray(fetch.Serialize()), "text/plain");
        }

        return storageService.DeleteFile(filePath);
    }

    private Task<StorageServiceFile> StoreBagInfo(DatasetVersionIdentifier datasetVersion, byte[] contents)
    {
        string filePath = GetBagInfoFilePath(datasetVersion);

        return storageService.StoreFile(filePath, CreateStreamFromByteArray(contents), "text/plain");
    }

    private static string GetActualFilePath(DatasetVersionIdentifier datasetVersion, BagItFetch fetch, string filePath)
    {
        if (fetch.TryGetItem(filePath, out var fetchItem))
        {
            return GetDatasetPath(datasetVersion) + DecodeUrlEncodedPath(fetchItem.Url[2..]);
        }

        return GetFullFilePath(datasetVersion, filePath);
    }

    private async IAsyncEnumerable<StorageServiceFile> ListPayloadFiles(DatasetVersionIdentifier datasetVersion)
    {
        string path = GetDatasetVersionPath(datasetVersion);

        await foreach (var file in storageService.ListFiles(path + "/data/"))
        {
            yield return file with { Path = file.Path[(path.Length + 1)..] };
        }
    }

    private static StreamWithLength CreateStreamFromByteArray(byte[] data) => new(new MemoryStream(data), data.LongLength);

    private async Task<bool> VersionHasBeenPublished(DatasetVersionIdentifier datasetVersion)
    {
        using var data = await storageService.GetFileData(GetBagItFilePath(datasetVersion));

        return data != null;
    }

    private async Task ThrowIfHasBeenPublished(DatasetVersionIdentifier datasetVersion)
    {
        if (await VersionHasBeenPublished(datasetVersion))
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
