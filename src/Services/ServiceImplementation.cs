using ByteSizeLib;
using DorisStorageAdapter.Models;
using DorisStorageAdapter.Models.BagIt;
using DorisStorageAdapter.Services.Exceptions;
using DorisStorageAdapter.Services.Lock;
using DorisStorageAdapter.Services.Storage;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services;

public class ServiceImplementation(
    IStorageService storageService,
    ILockService lockService,
    IOptions<GeneralConfiguration> generalConfiguration)
{
    private readonly IStorageService storageService = storageService;
    private readonly ILockService lockService = lockService;

    private readonly GeneralConfiguration generalConfiguration = generalConfiguration.Value;

    private const string payloadManifestSha256FileName = "manifest-sha256.txt";
    private const string tagManifestSha256FileName = "tagmanifest-sha256.txt";
    private const string fetchFileName = "fetch.txt";
    private const string bagItFileName = "bagit.txt";
    private const string bagInfoFileName = "bag-info.txt";

    public async Task SetupDatasetVersion(
        DatasetVersionIdentifier datasetVersion, 
        CancellationToken cancellationToken)
    {
        bool lockSuccessful = await lockService.TryLockDatasetVersionExclusive(datasetVersion, async () =>
        {
            await ThrowIfHasBeenPublished(datasetVersion, cancellationToken);
            await SetupDatasetVersionImpl(datasetVersion, cancellationToken);
        },
        cancellationToken);

        if (!lockSuccessful)
        {
            throw new ConflictException();
        }
    }

    private async Task SetupDatasetVersionImpl(
        DatasetVersionIdentifier datasetVersion, 
        CancellationToken cancellationToken)
    {
        async Task CopyPayloadManifest(DatasetVersionIdentifier fromVersion, DatasetVersionIdentifier toVersion)
        {
            using var fileData = await storageService.GetFileData(
                GetManifestFilePath(fromVersion, true), cancellationToken);

            if (fileData != null)
            {
                await storageService.StoreFile(
                    GetManifestFilePath(toVersion, true), fileData, cancellationToken);
            }
        }

        if (await storageService.ListFiles(GetDatasetVersionPath(datasetVersion), cancellationToken)
                .GetAsyncEnumerator(cancellationToken).MoveNextAsync())
        {
            // Files are already present for datasetVersion, abort
            return;
        }

        if (!TryGetPreviousVersionNumber(datasetVersion.VersionNumber, out string previousVersionNr))
        {
            return;
        }

        var previousVersion = new DatasetVersionIdentifier(datasetVersion.DatasetIdentifier, previousVersionNr);
        var fetch = await LoadFetch(previousVersion, cancellationToken);
        var newFetch = new BagItFetch();

        await CopyPayloadManifest(previousVersion, datasetVersion);

        string previousVersionUrl = "../" + UrlEncodePath(GetVersionPath(previousVersion)) + '/';

        foreach (var item in fetch.Items)
        {
            newFetch.AddOrUpdateItem(item);
        }

        await foreach (var file in ListPayloadFiles(previousVersion, CancellationToken.None))
        {
            if (!fetch.Contains(file.Path))
            {
                newFetch.AddOrUpdateItem(new(file.Path, file.Length, previousVersionUrl + UrlEncodePath(file.Path)));
            }
        }

        await StoreFetch(datasetVersion, newFetch, CancellationToken.None);
    }

    public async Task PublishDatasetVersion(
        DatasetVersionIdentifier datasetVersion, 
        AccessRightEnum accessRight, 
        string doi,
        CancellationToken cancellationToken)
    {
        bool lockSuccessful = await lockService.TryLockDatasetVersionExclusive(datasetVersion, async () =>
        {
            await PublishDatasetVersionImpl(datasetVersion, accessRight, doi, cancellationToken);
        },
        cancellationToken);

        if (!lockSuccessful)
        {
            throw new ConflictException();
        }
    }

    private async Task PublishDatasetVersionImpl(
        DatasetVersionIdentifier datasetVersion, 
        AccessRightEnum accessRight, 
        string doi,
        CancellationToken cancellationToken)
    {
        async Task<(T, byte[] Checksum)?> LoadWithChecksum<T>(string filePath, Func<Stream, CancellationToken, Task<T>> func)
        {
            using var fileData = await storageService.GetFileData(filePath, cancellationToken);

            if (fileData == null)
            {
                return null;
            }

            using var sha256 = SHA256.Create();
            using var hashStream = new CryptoStream(fileData.Stream, sha256, CryptoStreamMode.Read);

            return (await func(hashStream, cancellationToken), sha256.Hash!);
        }

        Task<(BagItManifest Manifest, byte[] Checksum)?> LoadManifestWithChecksum() =>
            LoadWithChecksum(GetManifestFilePath(datasetVersion, true), BagItManifest.Parse);

        Task<(BagItFetch Fetch, byte[] Checksum)?> LoadFetchWithChecksum() =>
            LoadWithChecksum(GetFetchFilePath(datasetVersion), BagItFetch.Parse);

        var fetch = await LoadFetchWithChecksum();
        long octetCount = 0;
        bool payloadFileFound = false;
        await foreach (var file in ListPayloadFiles(datasetVersion, cancellationToken))
        {
            payloadFileFound = true;
            octetCount += file.Length;
        }
        foreach (var item in fetch?.Fetch?.Items ?? [])
        {
            payloadFileFound = true;
            if (item.Length != null)
            {
                octetCount += item.Length.Value;
            }
        }

        if (!payloadFileFound)
        {
            // No payload files found, abort
            return;
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
        var tagManifest = await LoadManifest(datasetVersion, false, cancellationToken);
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

        await StoreManifest(datasetVersion, false, tagManifest, cancellationToken);
        await StoreBagInfo(datasetVersion, bagInfoContents, CancellationToken.None);
        await storageService.StoreFile(
            GetBagItFilePath(datasetVersion), 
            CreateFileDataFromByteArray(bagItContents),
            CancellationToken.None);
    }

    public async Task WithdrawDatasetVersion(
        DatasetVersionIdentifier datasetVersion, 
        CancellationToken cancellationToken)
    {
        bool lockSuccessful = await lockService.TryLockDatasetVersionExclusive(datasetVersion, async () =>
        {
            if (!await VersionHasBeenPublished(datasetVersion, cancellationToken))
            {
                throw new DatasetStatusException();
            }

            await WithdrawDatasetVersionImpl(datasetVersion, cancellationToken);
        },
        cancellationToken);

        if (!lockSuccessful)
        {
            throw new ConflictException();
        }
    }

    private async Task WithdrawDatasetVersionImpl(
        DatasetVersionIdentifier datasetVersion,
        CancellationToken cancellationToken)
    {
        var bagInfo = await LoadBagInfo(datasetVersion, cancellationToken);

        if (bagInfo == null)
        {
            // Do we need to throw an exception here?
            return;
        }

        bagInfo.DatasetStatus = DatasetStatusEnum.withdrawn;

        var bagInfoContents = bagInfo.Serialize();

        var tagManifest = await LoadManifest(datasetVersion, false, cancellationToken);
        tagManifest.AddOrUpdateItem(new(bagInfoFileName, SHA256.HashData(bagInfoContents)));
        await StoreManifest(datasetVersion, false, tagManifest, cancellationToken);

        await StoreBagInfo(datasetVersion, bagInfoContents, CancellationToken.None);
    }

    public async Task<Models.File> StoreFile(
        DatasetVersionIdentifier datasetVersion,
        FileTypeEnum type,
        string filePath,
        FileData data,
        CancellationToken cancellationToken)
    {
        filePath = GetFilePathOrThrow(type, filePath);
        Models.File? result = default;

        bool lockSuccessful = false;
        await lockService.TryLockDatasetVersionShared(datasetVersion, async () =>
        {
            string fullFilePath = GetFullFilePath(datasetVersion, filePath);

            lockSuccessful = await lockService.TryLockPath(fullFilePath, async () =>
            {
                await ThrowIfHasBeenPublished(datasetVersion, cancellationToken);
                result = await StoreFileImpl(
                    datasetVersion, 
                    filePath, 
                    fullFilePath, 
                    data, 
                    cancellationToken);
            },
            cancellationToken);

        },
        cancellationToken);

        if (!lockSuccessful)
        {
            throw new ConflictException();
        }

        return result!;
    }

    private async Task<Models.File> StoreFileImpl(
        DatasetVersionIdentifier datasetVersion,
        string filePath,
        string fullFilePath,
        FileData data,
        CancellationToken cancellationToken)
    {
        async Task<string?> Deduplicate(byte[] checksum)
        {
            if (!TryGetPreviousVersionNumber(datasetVersion.VersionNumber, out var prevVersionNr))
            {
                return null;
            }

            var prevVersion = new DatasetVersionIdentifier(datasetVersion.DatasetIdentifier, prevVersionNr);
            var prevManifest = await LoadManifest(prevVersion, true, CancellationToken.None);
            var itemsWithEqualChecksum = prevManifest.GetItemsByChecksum(checksum);

            if (!itemsWithEqualChecksum.Any())
            {
                return null;
            }

            var prevFetch = await LoadFetch(prevVersion, CancellationToken.None);

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

        var hashStream = new CountedHashStream(data.Stream);
        var result = await storageService.StoreFile(
            fullFilePath, 
            data with { Stream = hashStream }, 
            cancellationToken);

        // From this point on we do not want to cancel the operation,
        // since the file has been successfully stored.

        byte[] checksum = hashStream.GetHash();
        long bytesRead = hashStream.BytesRead;

        string? url = await Deduplicate(checksum);
        if (url != null)
        {
            // Deduplication was successful, store in fetch and delete uploaded file
            await AddOrUpdateFetchItem(datasetVersion, new(filePath, bytesRead, url), CancellationToken.None);
            await storageService.DeleteFile(fullFilePath, CancellationToken.None);
        }
        else
        {
            // File is not a duplicate, remove from fetch if present there
            await RemoveItemFromFetch(datasetVersion, filePath, CancellationToken.None);
        }

        // Update payload manifest
        await AddOrUpdatePayloadManifestItem(datasetVersion, new(filePath, checksum), CancellationToken.None);

        return ToModelFile(datasetVersion, new(
            ContentType: result.ContentType,
            DateCreated: result.DateCreated,
            DateModified: result.DateModified,
            Path: filePath,
            Length: bytesRead),
        checksum);
    }

    public async Task DeleteFile(
        DatasetVersionIdentifier datasetVersion,
        FileTypeEnum type,
        string filePath,
        CancellationToken cancellationToken)
    {
        filePath = GetFilePathOrThrow(type, filePath);

        bool lockSuccessful = false;
        await lockService.TryLockDatasetVersionShared(datasetVersion, async () =>
        {
            string fullFilePath = GetFullFilePath(datasetVersion, filePath);

            lockSuccessful = await lockService.TryLockPath(fullFilePath, async () =>
            {
                await ThrowIfHasBeenPublished(datasetVersion, cancellationToken);
                await DeleteFileImpl(datasetVersion, filePath, fullFilePath, cancellationToken);
            },
            cancellationToken);
        },
        cancellationToken);

        if (!lockSuccessful)
        {
            throw new ConflictException();
        }
    }

    private async Task DeleteFileImpl(
        DatasetVersionIdentifier datasetVersion,
        string filePath,
        string fullFilePath,
        CancellationToken cancellationToken)
    {
        await storageService.DeleteFile(fullFilePath, cancellationToken);

        // From this point on we do not want to cancel the operation,
        // since the file has been successfully deleted.

        await RemoveItemFromPayloadManifest(datasetVersion, filePath, CancellationToken.None);
        await RemoveItemFromFetch(datasetVersion, filePath, CancellationToken.None);
    }

    public async Task<FileData?> GetFileData(
        DatasetVersionIdentifier datasetVersion,
        FileTypeEnum type,
        string filePath,
        bool restrictToPubliclyAccessible,
        CancellationToken cancellationToken)
    {
        // Should we add some kind of locking here?
        // The requested file could potentially be added to fetch and removed from current version
        // after we found it in fetch and try to load it from current version, which will return
        // not found to the caller.

        filePath = GetFilePathOrThrow(type, filePath);

        if (restrictToPubliclyAccessible)
        {
            // Do not return file data unless dataset version is published (and not withdrawn),
            // and file type is either documentation (which entails publically accessible) or
            // access right is public.

            var bagInfo = await LoadBagInfo(datasetVersion, cancellationToken);

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

        var fetch = await LoadFetch(datasetVersion, cancellationToken);
        var result = await storageService.GetFileData(
            GetActualFilePath(datasetVersion, fetch, filePath), 
            cancellationToken);

        if (result != null && result.ContentType == null)
        {
            result = result with { ContentType = MimeTypes.GetMimeType(filePath) };
        }

        return result;
    }

    public async IAsyncEnumerable<Models.File> ListFiles(
        DatasetVersionIdentifier datasetVersion,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Should we add some kind of locking here?
        // Checksums and fetch can potentially be changed while processing this request,
        // leading to returning faulty checksums and other problems.

        var payloadManifest = await LoadManifest(datasetVersion, true, cancellationToken);
        var fetch = await LoadFetch(datasetVersion, cancellationToken);

        byte[]? GetChecksum(string filePath) =>
            payloadManifest.TryGetItem(filePath, out var value) ? value.Checksum : null;

        string datasetPath = GetDatasetPath(datasetVersion);
        var result = new List<StorageServiceFile>();

        string previousPayloadPath = "";
        Dictionary<string, StorageServiceFile> dict = [];
        foreach (var item in fetch.Items.OrderBy(i => i.Url, StringComparer.Ordinal))
        {
            string path = datasetPath + DecodeUrlEncodedPath(item.Url[3..]);
            string payloadPath = path[..(path.IndexOf("/data/") + 6)];

            if (payloadPath != previousPayloadPath)
            {
                dict = [];
                await foreach (var file in storageService.ListFiles(payloadPath, cancellationToken))
                {
                    dict[file.Path] = file;
                }
            }

            result.Add(dict[path] with { Path = item.FilePath });

            previousPayloadPath = payloadPath;
        }

        await foreach (var file in ListPayloadFiles(datasetVersion, cancellationToken))
        {
            result.Add(file);
        }

        foreach (var file in result.OrderBy(f => f.Path, StringComparer.InvariantCulture))
        {
            yield return ToModelFile(datasetVersion, file, GetChecksum(file.Path));
        }
    }

    public async Task WriteFileDataAsZip(
        DatasetVersionIdentifier datasetVersion, 
        string[] paths, 
        Stream stream,
        CancellationToken cancellationToken)
    {
        var payloadManifest = await LoadManifest(datasetVersion, true, cancellationToken);
        var fetch = await LoadFetch(datasetVersion, cancellationToken);

        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, false);

        foreach (var manifestFilePath in payloadManifest.Items.Select(i => i.FilePath))
        {
            string filePath = manifestFilePath[5..]; // Strip "data/"

            if (paths.Length > 0 && !paths.Any(filePath.StartsWith))
            {
                continue;
            }

            string actualFilePath = GetActualFilePath(datasetVersion, fetch, manifestFilePath);
            var data = await storageService.GetFileData(actualFilePath, cancellationToken);

            if (data != null)
            {
                var entry = archive.CreateEntry(filePath, CompressionLevel.NoCompression);
                using var entryStream = entry.Open();
                using var dataStream = data.Stream;
                await dataStream.CopyToAsync(entryStream, cancellationToken);
            }
        }
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

        return "data/" + type + '/' + filePath;
    }

    private static string GetDatasetPath(DatasetVersionIdentifier datasetVersion) =>
        datasetVersion.DatasetIdentifier + '/';

    private static string GetVersionPath(DatasetVersionIdentifier datasetVersion) =>
        datasetVersion.DatasetIdentifier + '-' + datasetVersion.VersionNumber;

    private static string GetDatasetVersionPath(DatasetVersionIdentifier datasetVersion) =>
        GetDatasetPath(datasetVersion) + GetVersionPath(datasetVersion) + '/';

    private static string GetFullFilePath(DatasetVersionIdentifier datasetVersion, string filePath) =>
        GetDatasetVersionPath(datasetVersion) + filePath;

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

    private static string UrlEncodePath(string path) =>
        string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    private static string DecodeUrlEncodedPath(string path) =>
        string.Join('/', path.Split('/').Select(Uri.UnescapeDataString));

    private Models.File ToModelFile(DatasetVersionIdentifier datasetVersion, StorageServiceFile file, byte[]? sha256)
    {
        FileTypeEnum type = default;
        string name = "";

        if (file.Path.StartsWith("data/data/"))
        {
            type = FileTypeEnum.data;
            name = file.Path["data/data/".Length..];
        }
        else if (file.Path.StartsWith("data/documentation/"))
        {
            type = FileTypeEnum.documentation;
            name = file.Path["data/documentation/".Length..];
        }

        return new()
        {
            Name = name,
            Type = type,
            ContentSize = file.Length,
            DateCreated = file.DateCreated,
            DateModified = file.DateModified,
            EncodingFormat = file.ContentType ?? MimeTypes.GetMimeType(file.Path),
            Sha256 = sha256 == null ? null : Convert.ToHexString(sha256),
            Url = new Uri(new Uri(generalConfiguration.PublicUrl), "file/" +
                UrlEncodePath(datasetVersion.DatasetIdentifier + '/' + datasetVersion.VersionNumber + '/' + type) +
                "?filePath=" + Uri.EscapeDataString(name))
        };
    }

    private async Task<BagItManifest> LoadManifest(
        DatasetVersionIdentifier datasetVersion, 
        bool payloadManifest,
        CancellationToken cancellationToken)
    {
        using var fileData = await storageService.GetFileData(
            GetManifestFilePath(datasetVersion, payloadManifest),
            cancellationToken);

        if (fileData == null)
        {
            return new();
        }

        return await BagItManifest.Parse(fileData.Stream, cancellationToken);
    }

    private async Task<BagItFetch> LoadFetch(
        DatasetVersionIdentifier datasetVersion, 
        CancellationToken cancellationToken)
    {
        using var fileData = await storageService.GetFileData
            (GetFetchFilePath(datasetVersion), 
            cancellationToken);

        if (fileData == null)
        {
            return new();
        }

        return await BagItFetch.Parse(fileData.Stream, cancellationToken);
    }

    private async Task<BagItInfo?> LoadBagInfo(DatasetVersionIdentifier datasetVersion, CancellationToken cancellationToken)
    {
        using var data = await storageService.GetFileData(
            GetBagInfoFilePath(datasetVersion), 
            cancellationToken);

        if (data == null)
        {
            return null;
        }

        return await BagItInfo.Parse(data.Stream, cancellationToken);
    }

    private Task AddOrUpdatePayloadManifestItem(
        DatasetVersionIdentifier datasetVersion, 
        BagItManifestItem item,
        CancellationToken cancellationToken) =>
        LockAndUpdatePayloadManifest(datasetVersion, manifest => manifest.AddOrUpdateItem(item), cancellationToken);

    private Task AddOrUpdateFetchItem(
        DatasetVersionIdentifier datasetVersion, 
        BagItFetchItem item,
        CancellationToken cancellationToken) =>
        LockAndUpdateFetch(datasetVersion, fetch => fetch.AddOrUpdateItem(item), cancellationToken);

    private Task RemoveItemFromPayloadManifest(
        DatasetVersionIdentifier datasetVersion, 
        string filePath,
        CancellationToken cancellationToken) =>
        LockAndUpdatePayloadManifest(datasetVersion, manifest => manifest.RemoveItem(filePath), cancellationToken);

    private Task RemoveItemFromFetch(
        DatasetVersionIdentifier datasetVersion, 
        string filePath,
        CancellationToken cancellationToken) =>
        LockAndUpdateFetch(datasetVersion, fetch => fetch.RemoveItem(filePath), cancellationToken);

    private async Task LockAndUpdatePayloadManifest(
        DatasetVersionIdentifier datasetVersion, 
        Func<BagItManifest, bool> action,
        CancellationToken cancellationToken)
    {
        // This method assumes that there is no exlusive lock on datasetVersion

        using (await lockService.LockPath(GetManifestFilePath(datasetVersion, true), cancellationToken))
        {
            var manifest = await LoadManifest(datasetVersion, true, cancellationToken);

            if (action(manifest))
            {
                await StoreManifest(datasetVersion, true, manifest, cancellationToken);
            }
        }
    }

    private async Task LockAndUpdateFetch(
        DatasetVersionIdentifier datasetVersion, 
        Func<BagItFetch, bool> action,
        CancellationToken cancellationToken)
    {
        // This method assumes that there is no exlusive lock on datasetVersion

        using (await lockService.LockPath(GetFullFilePath(datasetVersion, fetchFileName), cancellationToken))
        {
            var fetch = await LoadFetch(datasetVersion, cancellationToken);

            if (action(fetch))
            {
                await StoreFetch(datasetVersion, fetch, cancellationToken);
            }
        }
    }

    private Task StoreManifest(
        DatasetVersionIdentifier datasetVersion,
        bool payload,
        BagItManifest manifest,
        CancellationToken cancellationToken)
    {
        string filePath = GetManifestFilePath(datasetVersion, payload);

        if (manifest.Items.Any())
        {
            return storageService.StoreFile(
                filePath, 
                CreateFileDataFromByteArray(manifest.Serialize()), 
                cancellationToken);
        }

        return storageService.DeleteFile(filePath, cancellationToken);
    }

    private Task StoreFetch(
        DatasetVersionIdentifier datasetVersion, 
        BagItFetch fetch,
        CancellationToken cancellationToken)
    {
        string filePath = GetFetchFilePath(datasetVersion);

        if (fetch.Items.Any())
        {
            return storageService.StoreFile(
                filePath, 
                CreateFileDataFromByteArray(fetch.Serialize()),
                cancellationToken);
        }

        return storageService.DeleteFile(filePath, cancellationToken);
    }

    private Task<StorageServiceFileBase> StoreBagInfo(
        DatasetVersionIdentifier datasetVersion,
        byte[] contents,
        CancellationToken cancellationToken)
    {
        string filePath = GetBagInfoFilePath(datasetVersion);

        return storageService.StoreFile(filePath, CreateFileDataFromByteArray(contents), cancellationToken);
    }

    private static string GetActualFilePath(DatasetVersionIdentifier datasetVersion, BagItFetch fetch, string filePath)
    {
        if (fetch.TryGetItem(filePath, out var fetchItem))
        {
            return GetDatasetPath(datasetVersion) + DecodeUrlEncodedPath(fetchItem.Url[3..]);
        }

        return GetFullFilePath(datasetVersion, filePath);
    }

    private async IAsyncEnumerable<StorageServiceFile> ListPayloadFiles(
        DatasetVersionIdentifier datasetVersion,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string path = GetDatasetVersionPath(datasetVersion);

        await foreach (var file in storageService.ListFiles(path + "data/", cancellationToken))
        {
            yield return file with { Path = file.Path[path.Length..] };
        }
    }

    private static FileData CreateFileDataFromByteArray(byte[] data) =>
        new(new MemoryStream(data), data.LongLength, "text/plain");

    private async Task<bool> VersionHasBeenPublished(DatasetVersionIdentifier datasetVersion, CancellationToken cancellationToken)
    {
        using var data = await storageService.GetFileData(GetBagItFilePath(datasetVersion), cancellationToken);

        return data != null;
    }

    private async Task ThrowIfHasBeenPublished(DatasetVersionIdentifier datasetVersion, CancellationToken cancellationToken)
    {
        if (await VersionHasBeenPublished(datasetVersion, cancellationToken))
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
