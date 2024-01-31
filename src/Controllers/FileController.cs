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
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DatasetFileUpload.Controllers;

[ApiController]
public class FileController(ILogger<FileController> logger, IStorageService storageService) : Controller
{
    private const string payloadManifestSha256FileName = "manifest-sha256.txt";
    private const string tagManifestSha256FileName = "tagmanifest-sha256.txt";

    private readonly ILogger logger = logger;
    private readonly IStorageService storageService = storageService;

    [HttpPost("file/{datasetIdentifier}/{versionNumber}/{type}")]
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
                string fileName = contentDisposition.FileName.Value;

                if (!CheckFileName(fileName))
                {
                    return IllegalFileNameResult();
                }

                fileName = GetFileName(fileName, type);

                logger.LogInformation("Upload datasetIdentifier: {datasetIdentifier}, versionNumber: {versionNumber}:, FileName: {fileName}",
                    datasetIdentifier, versionNumber, fileName);

                try
                {
                    using var sha256 = SHA256.Create();
                    using var hashStream = new CryptoStream(section.Body, sha256, CryptoStreamMode.Read);

                    long bytesRead = 0;
                    using var monitoringStream = new MonitoringStream(hashStream);
                    monitoringStream.DidRead += (_, e) =>
                    {
                        bytesRead += e.Count;
                    };

                    var result = await storageService.StoreFile(datasetVersion, fileName, monitoringStream);

                    // Add file:
                    // Check if result.Sha256 is in manifest
                    //      Yes. Check if path is identical
                    //          Yes. Check if path is in fetch.txt.
                    //              Yes. Delete file.
                    //              No. Do nothing.
                    //          No. Add to fetch.txt and delete file.
                    //      No. Update manifest and remove from fetch (if present).

                    // Delete file:
                    // Delete from disk.
                    // Delete from manifest.
                    // Delete from fetch.txt.

                    // New version:
                    // Copy manifest from previous version
                    // Copy fetch.txt from previous version
                    // Complete fetch.txt with references to files in manifest but not in fetch

                    // Behöver struktur för att hämta utifrån checksumma i manifestet. Egen klass kanske?
                    // Varje checksumma kan ha flera filer kopplade till sig.
                    // string GetChecksum(string fileName)
                    // string[] GetFileNames(string checksum)

                    // För fetch behöver vi bara nyckel på filnamn
                    // Returnera både storlek och url?

                    // Om man i en ny version tar bort en fil (tas bort från fetch)
                    // och sedan laddar upp den igen, kommer den att lagras i båda nya versionen och föregående.
                    // Vill vi undvika detta måste vi ha ett gemensamt manifest över checksummorna för alla versioner
                    // som i OCFL.
                    // Samma sak om man inte importerar filer från förra versionen utan laddar upp dem igen.
                    // Ha ett gemensamt manifest, eller kanske titta på föregående versions manifest?
                    // Iterera över alla versioners manifest, blir inte skalbart?

                    await UpdateManifestSha256File(datasetVersion, result.Id, sha256.Hash!);

                    //result.Id = fileName; ??
                    result.ContentSize = bytesRead;
                    //result.EncodingFormat?
                    result.Sha256 = Convert.ToHexString(sha256.Hash!);
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
    public async Task<IActionResult> Delete(string datasetIdentifier, string versionNumber, UploadType type, string fileName)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        if (!CheckClaims(datasetVersion))
        {
            return Forbid();
        }

        if (!CheckFileName(fileName))
        {
            return IllegalFileNameResult();
        }

        fileName = GetFileName(fileName, type);

        logger.LogInformation("Delete datasetIdentifier: {datasetIdentifier}, versionNumber: {versionNumber}:, fileName: {fileName}",
            datasetIdentifier, versionNumber, fileName);

        try
        {
            await storageService.DeleteFile(datasetVersion, fileName);
            await UpdateManifestSha256File(datasetVersion, fileName, null);
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

        var payloadChecksums = await GetManifestSha256Values(datasetVersion, true);
        var tagChecksums = await GetManifestSha256Values(datasetVersion, false);

        static string? GetChecksum(BagItManifest manifest, string fileName) =>
            manifest.TryGetChecksum(fileName, out byte[] value) ? Convert.ToHexString(value) : null;

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
    public async Task<IActionResult> GetData(string datasetIdentifier, string versionNumber, UploadType type, string fileName)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        if (!CheckFileName(fileName))
        {
            return IllegalFileNameResult();
        }

        fileName = GetFileName(fileName, type);

        var fileData = await storageService.GetFileData(datasetVersion, fileName);

        if (fileData == null)
        {
            return NotFound();
        }

        Response.Headers.ContentLength = fileData.Length;

        return File(fileData.Stream, "application/octet-stream", fileName);
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

    private static string GetFileName(string fileName, UploadType type) =>
        type.ToString().ToLower() + '/' + fileName;


    private async Task<BagItManifest> GetManifestSha256Values(DatasetVersionIdentifier datasetVersion, bool payloadManifest)
    {
        var fileData = await storageService.GetFileData(datasetVersion,
            payloadManifest ? payloadManifestSha256FileName : tagManifestSha256FileName);

        if (fileData == null)
        {
            return new BagItManifest();
        }

        return await BagItManifest.Parse(fileData.Stream);
    }

    private async Task UpdateManifestSha256File(DatasetVersionIdentifier datasetVersion, string filePath, byte[]? sha256Hash)
    {
        bool payloadManifest = filePath.StartsWith("data/");
        var manifest = await GetManifestSha256Values(datasetVersion, payloadManifest);

        if (sha256Hash == null)
        {
            manifest.RemoveChecksum(filePath);
        }
        else
        {
            manifest.SetChecksum(filePath, sha256Hash);
        }
       
        string manifestFilePath = payloadManifest ? payloadManifestSha256FileName : tagManifestSha256FileName;

        if (manifest.IsEmpty())
        {
            await storageService.DeleteFile(datasetVersion, manifestFilePath);
        }
        else
        {
            await storageService.StoreFile(datasetVersion, manifestFilePath, new MemoryStream(manifest.Serialize()));
        }
    }
}
