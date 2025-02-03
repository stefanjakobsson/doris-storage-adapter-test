using ByteSizeLib;
using DorisStorageAdapter.Services.Contract;
using DorisStorageAdapter.Services.Contract.Exceptions;
using DorisStorageAdapter.Services.Contract.Models;
using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DorisStorageAdapter.Services.Implementation.BagIt;
using DorisStorageAdapter.Services.Implementation.BagIt.Fetch;
using DorisStorageAdapter.Services.Implementation.BagIt.Info;
using DorisStorageAdapter.Services.Implementation.BagIt.Manifest;
using DorisStorageAdapter.Services.Implementation.Lock;

namespace DorisStorageAdapter.Services.Implementation;

internal sealed class DatasetVersionService(
    ILockService lockService,
    MetadataService metadataService) : IDatasetVersionService
{
    private readonly ILockService lockService = lockService;
    private readonly MetadataService metadataService = metadataService;

    private static readonly byte[] bagItSha256 = SHA256.HashData(BagItDeclaration.Instance.Serialize());

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
            octetCount += file.Size;
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
            ExternalIdentifier = [doi],
            PayloadOxum = new(octetCount, payloadManifest?.BagItElement?.Items?.LongCount() ?? 0),
        };

        bagInfo.SetAccessRight(accessRight);
        bagInfo.SetDatasetStatus(DatasetStatus.completed);
        bagInfo.SetVersion(datasetVersion.Version);

        byte[] bagInfoContents = await metadataService.StoreBagItElement(datasetVersion, bagInfo, cancellationToken);

        // Add bagit.txt, bag-info.txt and manifest-sha256.txt to tagmanifest-sha256.txt
        var tagManifest = await metadataService.LoadBagItElement<BagItTagManifest>(datasetVersion, CancellationToken.None);
        tagManifest.AddOrUpdateItem(new(BagItDeclaration.FileName, bagItSha256));
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
        await metadataService.StoreBagItElement(datasetVersion, BagItDeclaration.Instance, CancellationToken.None);
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

        bagInfo.SetDatasetStatus(DatasetStatus.withdrawn);

        byte[] bagInfoContents = await metadataService.StoreBagItElement(datasetVersion, bagInfo, cancellationToken);

        var tagManifest = await metadataService.LoadBagItElement<BagItTagManifest>(datasetVersion, CancellationToken.None);
        tagManifest.AddOrUpdateItem(new(BagItInfo.FileName, SHA256.HashData(bagInfoContents)));
        await metadataService.StoreBagItElement(datasetVersion, tagManifest, CancellationToken.None);
    }
}
