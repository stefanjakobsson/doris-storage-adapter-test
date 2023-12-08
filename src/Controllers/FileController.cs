using DatasetFileUpload.Controllers.Filters;
using DatasetFileUpload.Models;
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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DatasetFileUpload.Controllers;

[ApiController]
[Authorize]
public class FileController : Controller
{
    private const string payloadManifestSha256FileName = "manifest-sha256.txt";
    private const string tagManifestSha256FileName = "tagmanifest-sha256.txt";

    private readonly ILogger logger;
    private readonly IStorageService storageService;

    public FileController(ILogger<FileController> logger, IStorageService storageService)
    {
        this.logger = logger;
        this.storageService = storageService;
    }

    [HttpPost("file/{datasetIdentifier}/{versionNumber}/{type}")]
    [Authorize(Roles = "User", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    // Disable form value model binding to ensure that files are not buffered
    [DisableFormValueModelBinding]
    // Disable request size limit to allow streaming large files
    [DisableRequestSizeLimit]
    public async Task<ActionResult<RoCrateFile>> Upload(string datasetIdentifier, string versionNumber, UploadType type)
    {
        if (!CheckClaims(datasetIdentifier, versionNumber))
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

                fileName = type.ToString().ToLower() + "/" + fileName;

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

                    var result = await storageService.StoreFile(datasetIdentifier, versionNumber, fileName, monitoringStream);

                    await UpdateManifestSha256File(datasetIdentifier, versionNumber, result.Id, sha256.Hash!);

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
        if (!CheckClaims(datasetIdentifier, versionNumber))
        {
            return Forbid();
        }

        if (!CheckFileName(fileName))
        {
            return IllegalFileNameResult();
        }

        fileName = type.ToString().ToLower() + "/" + fileName;

        logger.LogInformation("Delete datasetIdentifier: {datasetIdentifier}, versionNumber: {versionNumber}:, fileName: {fileName}", 
            datasetIdentifier, versionNumber, fileName);

        try
        {
            await storageService.DeleteFile(datasetIdentifier, versionNumber, fileName);
            await UpdateManifestSha256File(datasetIdentifier, versionNumber, fileName, null);
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
        var payloadChecksums = await GetManifestSha256Values(datasetIdentifier, versionNumber, true);
        var tagChecksums = await GetManifestSha256Values(datasetIdentifier, versionNumber, false);

        static string? GetChecksum(IDictionary<string, string> manifest, string fileName) =>
            manifest.TryGetValue(fileName, out string? value) ? value : null;

        await foreach (var file in storageService.ListFiles(datasetIdentifier, versionNumber))
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

    private bool CheckClaims(string datasetIdentifier, string versionNumber) =>
        HttpContext.User.Identity is ClaimsIdentity identity &&
        identity.FindFirst("DatasetIdentifier")?.Value == datasetIdentifier &&
        identity.FindFirst("VersionNumber")?.Value == versionNumber;

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

    [return: NotNull]
    private async Task<IDictionary<string, string>> GetManifestSha256Values(
        string datasetIdentifier, 
        string versionNumber,
        bool payloadManifest)
    {
        var result = new Dictionary<string, string>();

        var stream = await storageService.GetFileData(datasetIdentifier, versionNumber,
            payloadManifest ? payloadManifestSha256FileName : tagManifestSha256FileName);

        if (stream == null)
        {
            return result;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
        {
            int index = line.IndexOf(' ');
            string hash = line[..index];
            string fileName = line[(index + 1)..];

            result[fileName] = hash;
        }

        return result;
    }

    private async Task UpdateManifestSha256File(
        string datasetIdentifier, 
        string versionNumber,
        string filePath, 
        byte[]? sha256Hash)
    {
        static string PercentEncodePath(string path)
        {
            return path
                .Replace("%", "%25")
                .Replace("\n", "%0A")
                .Replace("\r", "%0D");
        }

        bool payloadManifest = filePath.StartsWith("data/");
        var values = await GetManifestSha256Values(datasetIdentifier, versionNumber, payloadManifest);
        var encodedFilePath = PercentEncodePath(filePath);

        if (sha256Hash != null)
        {
            values[encodedFilePath] = Convert.ToHexString(sha256Hash).ToLower();
        }
        else
        {
            values.Remove(encodedFilePath);
        }

        string manifestFilePath = payloadManifest ? payloadManifestSha256FileName : tagManifestSha256FileName;

        if (values.Any())
        {
            var newContent = Encoding.UTF8.GetBytes(string.Join("\n", values.Select(k => k.Value + " " + k.Key)));

            await storageService.StoreFile(datasetIdentifier, versionNumber, manifestFilePath, new MemoryStream(newContent));
        }
        else
        {
            await storageService.DeleteFile(datasetIdentifier, versionNumber, manifestFilePath);
        }
    }
}
