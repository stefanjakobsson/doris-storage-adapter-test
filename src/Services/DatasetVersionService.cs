using ByteSizeLib;
using DorisStorageAdapter.Models;
using DorisStorageAdapter.Services.BagIt;
using DorisStorageAdapter.Services.Exceptions;
using DorisStorageAdapter.Services.Lock;
using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services;

internal sealed class DatasetVersionService(
    ILockService lockService,
    MetadataService metadataService): IDatasetVersionService
{
    private readonly ILockService lockService = lockService;
    private readonly MetadataService metadataService = metadataService;

    private static readonly byte[] bagItSha256 = SHA256.HashData(BagIt.BagIt.Instance.Serialize());

    public async Task PublishDatasetVersion(
        DatasetVersion datasetVersion,
        AccessRight accessRight,
        string doi,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(datasetVersion);
        ArgumentException.ThrowIfNullOrEmpty(doi);

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
        DatasetVersion datasetVersion,
        AccessRight accessRight,
        string doi,
        CancellationToken cancellationToken)
    {
        var fetch = await metadataService.LoadBagItElementWithChecksum<BagItFetch>(datasetVersion, cancellationToken);
        long octetCount = 0;
        bool payloadFileFound = false;
        await foreach (var file in metadataService.ListPayloadFiles(datasetVersion, null, cancellationToken))
        {
            payloadFileFound = true;
            octetCount += file.Length;
        }
        foreach (var item in fetch?.BagItElement?.Items ?? [])
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

        var payloadManifest = await metadataService
            .LoadBagItElementWithChecksum<BagItPayloadManifest>(datasetVersion, cancellationToken);

        var bagInfo = new BagItInfo
        {
            BaggingDate = DateTime.UtcNow,
            BagGroupIdentifier = datasetVersion.Identifier,
            BagSize = ByteSize.FromBytes(octetCount).ToBinaryString(CultureInfo.InvariantCulture),
            ExternalIdentifier = doi,
            PayloadOxum = new(octetCount, payloadManifest?.BagItElement?.Items?.LongCount() ?? 0),
            AccessRight = accessRight,
            DatasetStatus = DatasetStatus.completed,
            Version = datasetVersion.Version
        };

        byte[] bagInfoContents = await metadataService.StoreBagItElement(datasetVersion, bagInfo, cancellationToken);

        // Add bagit.txt, bag-info.txt and manifest-sha256.txt to tagmanifest-sha256.txt
        var tagManifest = await metadataService.LoadBagItElement<BagItTagManifest>(datasetVersion, CancellationToken.None);
        tagManifest.AddOrUpdateItem(new(BagIt.BagIt.FileName, bagItSha256));
        tagManifest.AddOrUpdateItem(new(BagItInfo.FileName, SHA256.HashData(bagInfoContents)));
        if (payloadManifest != null)
        {
            tagManifest.AddOrUpdateItem(new(BagItPayloadManifest.FileName, payloadManifest.Value.Checksum));
        }
        if (fetch != null)
        {
            tagManifest.AddOrUpdateItem(new(BagItFetch.FileName, fetch.Value.Checksum));
        }

        await metadataService.StoreBagItElement(datasetVersion, tagManifest, CancellationToken.None);
        await metadataService.StoreBagItElement(datasetVersion, BagIt.BagIt.Instance, CancellationToken.None);
    }

    public async Task WithdrawDatasetVersion(
        DatasetVersion datasetVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(datasetVersion);

        bool lockSuccessful = await lockService.TryLockDatasetVersionExclusive(datasetVersion, async () =>
        {
            if (!await metadataService.VersionHasBeenPublished(datasetVersion, cancellationToken))
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
        DatasetVersion datasetVersion,
        CancellationToken cancellationToken)
    {
        var bagInfo = await metadataService.LoadBagItElement<BagItInfo>(datasetVersion, cancellationToken);

        if (!bagInfo.HasValues())
        {
            // Throw exception here?
            return;
        }

        bagInfo.DatasetStatus = DatasetStatus.withdrawn;

        byte[] bagInfoContents = await metadataService.StoreBagItElement(datasetVersion, bagInfo, cancellationToken);

        var tagManifest = await metadataService.LoadBagItElement<BagItTagManifest>(datasetVersion, CancellationToken.None);
        tagManifest.AddOrUpdateItem(new(BagItInfo.FileName, SHA256.HashData(bagInfoContents)));
        await metadataService.StoreBagItElement(datasetVersion, tagManifest, CancellationToken.None);
    }
}
