using DatasetFileUpload.Controllers.Filters;
using DatasetFileUpload.Models;
using DatasetFileUpload.Services;
using DatasetFileUpload.Services.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DatasetFileUpload.Controllers;

[ApiController]
public class FileController(ILogger<FileController> logger, IStorageService storageService) : Controller
{
    private readonly ILogger logger = logger;
    private readonly IStorageService storageService = storageService;

    [HttpPut("file/{datasetIdentifier}/{versionNumber}")]
    [Authorize(Roles = "UploadService", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> SetupVersion(string datasetIdentifier, string versionNumber)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        await new FileServiceImplementation(storageService).SetupVersion(datasetVersion);

        return Ok();
    }

    [HttpPut("file/{datasetIdentifier}/{versionNumber}/{type}")]
    [Authorize(Roles = "User", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    // Disable form value model binding to ensure that files are not buffered
    [DisableFormValueModelBinding]
    // Disable request size limit to allow streaming large files
    [DisableRequestSizeLimit]
    public async Task<ActionResult<RoCrateFile>> Upload(string datasetIdentifier, string versionNumber, FileType type)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        if (!CheckClaims(datasetVersion))
        {
            return Forbid();
        }

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

                logger.LogDebug("Upload datasetIdentifier: {datasetIdentifier}, versionNumber: {versionNumber}:, type: {type}, filePath: {filePath}",
                    datasetIdentifier, versionNumber, type, filePath);

                try
                {                  
                    return await new FileServiceImplementation(storageService).Upload(datasetVersion, type, filePath, section.Body);
                }
                catch (IllegalPathException)
                {
                    return IllegalFilePathResult();
                }
            }

            section = await reader.ReadNextSectionAsync();
        }

        return Problem("No file posted.", statusCode: 400);
    }


    [HttpDelete("file/{datasetIdentifier}/{versionNumber}/{type}")]
    [Authorize(Roles = "User", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Delete(string datasetIdentifier, string versionNumber, FileType type, string filePath)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        if (!CheckClaims(datasetVersion))
        {
            return Forbid();
        }

        logger.LogDebug("Delete datasetIdentifier: {datasetIdentifier}, versionNumber: {versionNumber}:, type: {type}, filePath: {filePath}",
            datasetIdentifier, versionNumber, type, filePath);

        try
        {
            await new FileServiceImplementation(storageService).Delete(datasetVersion, type, filePath);
        }
        catch (IllegalPathException)
        {
            return IllegalFilePathResult();
        }

        return Ok();
    }

    [HttpGet("/file/{datasetIdentifier}/{versionNumber}/{type}")]
    public async Task<IActionResult> GetData(string datasetIdentifier, string versionNumber, FileType type, string filePath)
    {
        logger.LogDebug("GetData datasetIdentifier: {datasetIdentifier}, versionNumber: {versionNumber}, type: {type}, filePath: {filePath}",
            datasetIdentifier, versionNumber, type, filePath);

        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        try
        {
            var fileData = await new FileServiceImplementation(storageService).GetData(datasetVersion, type, filePath);

            if (fileData == null)
            {
                return NotFound();
            }

            Response.Headers.ContentLength = fileData.Length;

            return File(fileData.Stream, "application/octet-stream", filePath);
        }
        catch (IllegalPathException)
        {
            return IllegalFilePathResult();
        }
    }

    [HttpGet("/file/{datasetIdentifier}/{versionNumber}")]
    [Authorize(Roles = "UploadService", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async IAsyncEnumerable<RoCrateFile> ListFiles(string datasetIdentifier, string versionNumber)
    {
        logger.LogDebug("ListFiles datasetIdentifier: {datasetIdentifier}, versionNumber: {versionNumber}",
            datasetIdentifier, versionNumber);

        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        await foreach (var file in new FileServiceImplementation(storageService).ListFiles(datasetVersion))
        {
            yield return file;
        }
    }

    private bool CheckClaims(DatasetVersionIdentifier datasetVersion) =>
        HttpContext.User.Identity is ClaimsIdentity identity &&
        identity.FindFirst("DatasetIdentifier")?.Value == datasetVersion.DatasetIdentifier &&
        identity.FindFirst("VersionNumber")?.Value == datasetVersion.VersionNumber;

    private ObjectResult IllegalFilePathResult() =>
        Problem("Illegal file path.", statusCode: 400);
}
