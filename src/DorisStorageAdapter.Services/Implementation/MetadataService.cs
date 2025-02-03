using DorisStorageAdapter.Services.Contract.Models;
using DorisStorageAdapter.Services.Implementation.BagIt;
using DorisStorageAdapter.Services.Implementation.Storage;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services.Implementation;

internal sealed class MetadataService(IStorageService storageService)
{
    private readonly IStorageService storageService = storageService;

    public async Task<T> LoadBagItElement<T>(
        DatasetVersion datasetVersion, CancellationToken cancellationToken)
        where T : class, IBagItElement<T>, new()
    {
        var fileData = await GetBagItElementFileData<T>(datasetVersion, cancellationToken);

        if (fileData == null)
        {
            return new();
        }

        using (fileData.Stream)
        {
            return await T.Parse(fileData.Stream, cancellationToken);
        }
    }

    public async Task<(T BagItElement, byte[] Checksum)?> LoadBagItElementWithChecksum<T>(
        DatasetVersion datasetVersion, CancellationToken cancellationToken)
        where T : IBagItElement<T>
    {
        var fileData = await GetBagItElementFileData<T>(datasetVersion, cancellationToken);

        if (fileData == null)
        {
            return null;
        }

        using var hashStream = new CountedHashStream(fileData.Stream);
        return (await T.Parse(hashStream, cancellationToken), hashStream.GetHash());
    }

    public async Task<byte[]> StoreBagItElement<T>(
        DatasetVersion datasetVersion, T element, CancellationToken cancellationToken)
        where T : IBagItElement<T>
    {
        string filePath = Paths.GetFullFilePath(datasetVersion, T.FileName);

        if (element.HasValues())
        {
            var bytes = element.Serialize();

            using var stream = new MemoryStream(bytes);
            await storageService.StoreFile(
                filePath,
                stream,
                stream.Length,
                "text/plain",
                cancellationToken);

            return bytes;
        }

        await storageService.DeleteFile(filePath, cancellationToken);
        return [];
    }

    public async IAsyncEnumerable<StorageFileMetadata> ListPayloadFiles(
        DatasetVersion datasetVersion,
        FileType? type,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string path = Paths.GetDatasetVersionPath(datasetVersion);

        await foreach (var file in storageService.ListFiles(
            path + Paths.GetPayloadPath(type),
            cancellationToken))
        {
            yield return file with { Path = file.Path[path.Length..] };
        }
    }

    public async Task<bool> VersionHasBeenPublished(DatasetVersion datasetVersion, CancellationToken cancellationToken) =>
        await storageService.GetFileMetadata(Paths.GetFullFilePath(datasetVersion, BagItDeclaration.FileName), cancellationToken) != null;

    private Task<StorageFileData?> GetBagItElementFileData<T>(
        DatasetVersion datasetVersion, CancellationToken cancellationToken)
        where T : IBagItElement<T> =>
        storageService.GetFileData(
            Paths.GetFullFilePath(datasetVersion, T.FileName), null, cancellationToken);
}
