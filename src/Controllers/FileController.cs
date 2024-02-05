using DatasetFileUpload.Controllers.Filters;
using DatasetFileUpload.Models;
using DatasetFileUpload.Models.BagIt;
using DatasetFileUpload.Services.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Nerdbank.Streams;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace DatasetFileUpload.Controllers;

[ApiController]
public class FileController(ILogger<FileController> logger, IStorageService storageService) : Controller
{
    private const string payloadManifestSha256FileName = "manifest-sha256.txt";
    private const string tagManifestSha256FileName = "tagmanifest-sha256.txt";
    private const string fetchFileName = "fetch.txt";

    private readonly ILogger logger = logger;
    private readonly IStorageService storageService = storageService;

    [HttpPut("file/{datasetIdentifier}/{versionNumber}")]
    [Authorize(Roles = "UploadService", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> SetupVersion(string datasetIdentifier, string versionNumber)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        // Must check to see if version already exists in storage, so that we do not overwrite?

        async Task CopyManifest(DatasetVersionIdentifier fromVersion, DatasetVersionIdentifier toVersion, bool payload)
        {
            var fileData = await storageService.GetFileData(fromVersion, GetManifestFilePath(payload));
            if (fileData != null)
            {
                await storageService.StoreFile(toVersion, GetManifestFilePath(payload), fileData.Stream);
            }
        }

        if (TryGetPreviousVersionNumber(versionNumber, out string previousVersionNr))
        {
            var previousVersion = new DatasetVersionIdentifier(datasetVersion.DatasetIdentifier, previousVersionNr);
            //var payloadManifest = await LoadManifest(previousVersion, true);
            //var tagManifest = await LoadManifest(previousVersion, false);
            var fetch = await LoadFetch(previousVersion);

            await CopyManifest(previousVersion, datasetVersion, true);
            await CopyManifest(previousVersion, datasetVersion, false);

            string previousVersionUrl = "../" + Uri.EscapeDataString(previousVersion.DatasetIdentifier + "-" +
                                        previousVersion.VersionNumber) + '/';

            // Maybe store all URL:s within the same version as ../ instead?
            foreach (var item in fetch.Items.Where(i => !i.Url.StartsWith("../")))
            {
                fetch.AddOrUpdateItem(item with { Url = previousVersionUrl + item.Url });
            }

            await foreach (var file in storageService.ListFiles(previousVersion))
            {
                if (!fetch.TryGetItem(file.Id, out var _))
                {
                    fetch.AddOrUpdateItem(new(file.Id, file.ContentSize, previousVersionUrl + Uri.EscapeDataString(file.Id)));
                }
            }

            /*foreach (var item in payloadManifest.Items.Where(i => !fetch.TryGetItem(i.FilePath, out var _)))
            {
                fetch.AddOrUpdateItem(new(item.FilePath, null, "../" + Uri.EscapeDataString(previousVersion.DatasetIdentifier + "-" +
                                        previousVersion.VersionNumber) + '/' + Uri.EscapeDataString(item.FilePath)));
            }

            foreach (var item in tagManifest.Items.Where(i => !fetch.TryGetItem(i.FilePath, out var _)))
            {
                fetch.AddOrUpdateItem(new(item.FilePath, null, "../" + Uri.EscapeDataString(previousVersion.DatasetIdentifier + "-" +
                                        previousVersion.VersionNumber) + '/' + Uri.EscapeDataString(item.FilePath)));
            }*/

            await StoreFetch(datasetVersion, fetch);
        }

        return Ok();
    }

    [HttpPut("file/{datasetIdentifier}/{versionNumber}/{type}")]
    [Authorize(Roles = "User", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    // Disable form value model binding to ensure that files are not buffered
    [DisableFormValueModelBinding]
    // Disable request size limit to allow streaming large files
    [DisableRequestSizeLimit]
    public async Task<ActionResult<RoCrateFile>> Upload(string datasetIdentifier, string versionNumber, UploadType type)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        if (!CheckClaims(datasetVersion))
        {
            return Forbid();
        }

        // check if current dataset version is not published (publicationDate is set in RO-Crate metadata)

        // if type === data check if RO-Crate metadata conditionsOfAccess is PUBLIC (file of type data needs to generate url)

        var request = HttpContext.Request;

        // Validation of Content-Type:
        // 1. It must be a form-data request
        // 2. A boundary should be found in the Content-Type
        if (!request.HasFormContentType ||
            !MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaTypeHeader) ||
            string.IsNullOrEmpty(mediaTypeHeader.Boundary.Value))
        {
            return new UnsupportedMediaTypeResult();
        }

        var boundary = HeaderUtilities.RemoveQuotes(mediaTypeHeader.Boundary.Value).Value!;
        var reader = new MultipartReader(boundary, request.Body);
        var section = await reader.ReadNextSectionAsync();

        while (section != null)
        {
            if (ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition) &&
                contentDisposition.DispositionType.Equals("form-data") &&
                !string.IsNullOrEmpty(contentDisposition.FileName.Value))
            {
                string filePath = contentDisposition.FileName.Value;

                if (!CheckFileName(filePath))
                {
                    return IllegalFileNameResult();
                }

                filePath = GetFilePath(filePath, type);

                logger.LogInformation("Upload datasetIdentifier: {datasetIdentifier}, versionNumber: {versionNumber}:, FileName: {filePath}",
                    datasetIdentifier, versionNumber, filePath);

                try
                {
                    static bool TryDeduplicate(
                        BagItManifestItem item,
                        DatasetVersionIdentifier currentVersion,
                        DatasetVersionIdentifier compareToVersion,
                        BagItManifest manifest,
                        BagItFetch fetch,
                        out string url)
                    {
                        var itemsWithEqualChecksum = manifest.GetItemsByChecksum(item.Checksum);

                        if (itemsWithEqualChecksum.Any())
                        {
                            // If we find an item with equal checksum in fetch.txt, use that URL
                            foreach (var candidate in itemsWithEqualChecksum)
                            {
                                if (fetch.TryGetItem(candidate.FilePath, out var fetchItem))
                                {
                                    url = fetchItem.Url;
                                    return true;
                                }
                            }

                            string relativePath = "";
                            if (currentVersion != compareToVersion)
                            {
                                relativePath = "../" + Uri.EscapeDataString(compareToVersion.DatasetIdentifier + "-" +
                                        compareToVersion.VersionNumber) + '/';
                            }

                            // Nothing found in fetch.txt, just take first item's file path
                            url = relativePath + Uri.EscapeDataString(itemsWithEqualChecksum.First().FilePath);
                            return true;
                        }

                        url = "";
                        return false;
                    }

                    using var sha256 = SHA256.Create();
                    using var hashStream = new CryptoStream(section.Body, sha256, CryptoStreamMode.Read);

                    long bytesRead = 0;
                    using var monitoringStream = new MonitoringStream(hashStream);
                    monitoringStream.DidRead += (_, e) =>
                    {
                        bytesRead += e.Count;
                    };

                    var result = await storageService.StoreFile(datasetVersion, filePath, monitoringStream);

                    byte[] checksum = sha256.Hash!;

                    var bagItItem = new BagItManifestItem(result.Id, sha256.Hash!);
                    var manifest = await LoadManifest(datasetVersion, type == UploadType.Data);
                    var fetch = await LoadFetch(datasetVersion);


                    if (!TryDeduplicate(bagItItem, datasetVersion, datasetVersion, manifest, fetch, out string url) &&
                        TryGetPreviousVersionNumber(datasetVersion.VersionNumber, out var prevVersionNr))
                    {
                        var prevVersion = new DatasetVersionIdentifier(datasetVersion.DatasetIdentifier, prevVersionNr);
                        var prevManifest = await LoadManifest(prevVersion, type == UploadType.Data);
                        var prevFetch = await LoadFetch(prevVersion);

                        TryDeduplicate(bagItItem, datasetVersion, prevVersion, prevManifest, prevFetch, out url);
                    }

                    if (url != "")
                    {
                        fetch.AddOrUpdateItem(new(bagItItem.FilePath, bytesRead, url));
                        await StoreFetch(datasetVersion, fetch);
                        await storageService.DeleteFile(datasetVersion, result.Id);
                    }
                    else
                    {
                        if (fetch.RemoveItem(bagItItem.FilePath))
                        {
                            if (fetch.Items.Any())
                            {
                                await StoreFetch(datasetVersion, fetch);
                            }
                            else
                            {
                                await storageService.DeleteFile(datasetVersion, fetchFileName);
                            }
                        }
                    }

                    manifest.AddOrUpdateItem(bagItItem);
                    await StoreManifest(datasetVersion, type == UploadType.Data, manifest);

                    //result.Id = fileName; ??
                    result.ContentSize = bytesRead;
                    //result.EncodingFormat?
                    result.Sha256 = Convert.ToHexString(checksum);
                    // result.Url?

                    return result;
                }
                catch (IllegalFileNameException)
                {
                    return IllegalFileNameResult();
                }
            }

            section = await reader.ReadNextSectionAsync();
        }

        return Problem("No file posted.", statusCode: 400);
    }


    [HttpDelete("file/{datasetIdentifier}/{versionNumber}/{type}")]
    [Authorize(Roles = "User", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Delete(string datasetIdentifier, string versionNumber, UploadType type, string filePath)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        if (!CheckClaims(datasetVersion))
        {
            return Forbid();
        }

        if (!CheckFileName(filePath))
        {
            return IllegalFileNameResult();
        }

        filePath = GetFilePath(filePath, type);

        logger.LogInformation("Delete datasetIdentifier: {datasetIdentifier}, versionNumber: {versionNumber}:, fileName: {fileName}",
            datasetIdentifier, versionNumber, filePath);

        try
        {
            await storageService.DeleteFile(datasetVersion, filePath);
            await RemoveItemFromManifest(datasetVersion, filePath);
            await RemoveItemFromFetch(datasetVersion, filePath);
        }
        catch (IllegalFileNameException)
        {
            return IllegalFileNameResult();
        }

        return Ok();
    }

    [HttpGet("/file/{datasetIdentifier}/{versionNumber}")]
    [Authorize(Roles = "UploadService", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async IAsyncEnumerable<RoCrateFile> ListFiles(string datasetIdentifier, string versionNumber)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        var payloadChecksums = await LoadManifest(datasetVersion, true);
        var tagChecksums = await LoadManifest(datasetVersion, false);

        static string? GetChecksum(BagItManifest manifest, string fileName) =>
            manifest.TryGetItem(fileName, out var value) ? Convert.ToHexString(value.Checksum) : null;

        await foreach (var file in storageService.ListFiles(datasetVersion))
        {
            if (file.Id.StartsWith("data/"))
            {
                file.Sha256 = GetChecksum(payloadChecksums, file.Id);
            }
            else
            {
                file.Sha256 = GetChecksum(tagChecksums, file.Id);
            }

            yield return file;
        }
    }

    [HttpGet("/file/{datasetIdentifier}/{versionNumber}/{type}")]
    public async Task<IActionResult> GetData(string datasetIdentifier, string versionNumber, UploadType type, string filePath)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        if (!CheckFileName(filePath))
        {
            return IllegalFileNameResult();
        }

        filePath = GetFilePath(filePath, type);

        var fileData = await storageService.GetFileData(datasetVersion, filePath);

        if (fileData == null)
        {
            return NotFound();
        }

        Response.Headers.ContentLength = fileData.Length;

        return File(fileData.Stream, "application/octet-stream", filePath);
    }

    private bool CheckClaims(DatasetVersionIdentifier datasetVersion) =>
        HttpContext.User.Identity is ClaimsIdentity identity &&
        identity.FindFirst("DatasetIdentifier")?.Value == datasetVersion.DatasetIdentifier &&
        identity.FindFirst("VersionNumber")?.Value == datasetVersion.VersionNumber;

    private static bool CheckFileName(string fileName)
    {
        foreach (string pathComponent in fileName.Split('/'))
        {
            if (pathComponent == "" ||
                pathComponent == "." ||
                pathComponent == "..")
            {
                return false;
            }
        }

        return true;
    }

    private ObjectResult IllegalFileNameResult() =>
        Problem("Illegal file name.", statusCode: 400);

    private static string GetFilePath(string fileName, UploadType type) =>
        type.ToString().ToLower() + '/' + fileName;


    private async Task<BagItManifest> LoadManifest(DatasetVersionIdentifier datasetVersion, bool payloadManifest)
    {
        var fileData = await storageService.GetFileData(datasetVersion, GetManifestFilePath(payloadManifest));

        if (fileData == null)
        {
            return new();
        }

        return await BagItManifest.Parse(fileData.Stream);
    }

    private async Task<BagItFetch> LoadFetch(DatasetVersionIdentifier datasetVersion)
    {
        var fileData = await storageService.GetFileData(datasetVersion, fetchFileName);

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
        manifest.AddOrUpdateItem(item);

        await StoreManifest(datasetVersion, payload, manifest);
    }

    private async Task AddOrUpdateFetchItem(DatasetVersionIdentifier datasetVersion, BagItFetchItem item)
    {
        var fetch = await LoadFetch(datasetVersion);
        fetch.AddOrUpdateItem(item);

        await StoreFetch(datasetVersion, fetch);
    }

    private async Task RemoveItemFromManifest(DatasetVersionIdentifier datasetVersion, string filePath)
    {
        bool payloadManifest = filePath.StartsWith("data/");
        var manifest = await LoadManifest(datasetVersion, payloadManifest);

        if (manifest.RemoveItem(filePath))
        {
            if (manifest.Items.Any())
            {
                await StoreManifest(datasetVersion, payloadManifest, manifest);
            }
            else
            {
                await storageService.DeleteFile(datasetVersion, GetManifestFilePath(payloadManifest));
            }
        }
    }

    private async Task RemoveItemFromFetch(DatasetVersionIdentifier datasetVersion, string filePath)
    {
        var fetch = await LoadFetch(datasetVersion);

        if (fetch.RemoveItem(filePath))
        {
            if (fetch.Items.Any())
            {
                await StoreFetch(datasetVersion, fetch);
            }
            else
            {
                await storageService.DeleteFile(datasetVersion, fetchFileName);
            }
        }
    }

    private static string GetManifestFilePath(bool payload) => payload ? payloadManifestSha256FileName : tagManifestSha256FileName;

    private Task<RoCrateFile> StoreManifest(DatasetVersionIdentifier datasetVersion, bool payload, BagItManifest manifest) =>
         storageService.StoreFile(datasetVersion, GetManifestFilePath(payload), new MemoryStream(manifest.Serialize()));

    private Task StoreFetch(DatasetVersionIdentifier datasetVersion, BagItFetch fetch) =>
        storageService.StoreFile(datasetVersion, fetchFileName, new MemoryStream(fetch.Serialize()));

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
