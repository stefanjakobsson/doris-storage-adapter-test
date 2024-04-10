using DatasetFileUpload.Models;
using DatasetFileUpload.Services;
using DatasetFileUpload.Services.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO.Compression;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DatasetFileUpload.Controllers;

[ApiController]
public class FileController(ILogger<FileController> logger, FileService fileService) : Controller
{
    private readonly ILogger logger = logger;
    private readonly FileService fileService = fileService;

    [HttpPut("file/{datasetIdentifier}/{versionNumber}")]
    [Authorize(Roles = "UploadService")]
    public async Task<IActionResult> SetupVersion(string datasetIdentifier, string versionNumber)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        try
        {
            await fileService.SetupVersion(datasetVersion);
        }
        catch (ConflictException)
        {
            return ConflictResult();
        }
        catch (DatasetStatusException)
        {
            return StatusMismatchResult();
        }

        return Ok();
    }

    [HttpPut("{datasetIdentifier}/{versionNumber}/publish")]
    [Authorize(Roles = "UploadService")]
    public async Task<IActionResult> PublishVersion(string datasetIdentifier, string versionNumber)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        try
        {
            await fileService.PublishVersion(datasetVersion, true, "test");
        }
        catch (ConflictException)
        {
            return ConflictResult();
        }

        return Ok();
    }

    [HttpPut("{datasetIdentifier}/{versionNumber}/withdraw")]
    [Authorize(Roles = "UploadService")]
    public async Task<IActionResult> WithdrawVersion(string datasetIdentifier, string versionNumber)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        try
        {
            await fileService.WithdrawVersion(datasetVersion);
        }
        catch (ConflictException)
        {
            return ConflictResult();
        }
        catch (DatasetStatusException)
        {
            return StatusMismatchResult();
        }

        return Ok();
    }

    /*[HttpPut("file/{datasetIdentifier}/{versionNumber}/{type}")]
    [Authorize(Roles = "User")]
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
                    return await fileService.Upload(datasetVersion, type, filePath, section.Body);
                }
                catch (IllegalPathException)
                {
                    return IllegalPathResult();
                }
                catch (ConflictException)
                {
                    return ConflictResult();
                }
                catch (DatasetStatusException)
                {
                    return StatusMismatchResult();
                }
            }

            section = await reader.ReadNextSectionAsync();
        }

        return Problem("No file posted.", statusCode: 400);
    }*/

    [HttpPut("file/{datasetIdentifier}/{versionNumber}/{type}")]
    [Authorize(Roles = "User")]
    // Disable request size limit to allow streaming large files
    [DisableRequestSizeLimit]
    public async Task<ActionResult<RoCrateFile>> Upload(
        string datasetIdentifier, 
        string versionNumber,
        FileType type, 
        [FromQuery] string filePath)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        if (!CheckClaims(datasetVersion))
        {
            return Forbid();
        }

        if (Request.Headers.ContentLength == null)
        {
            return Problem("Missing Content-Length.", statusCode: 400);
        }

        logger.LogDebug("Upload datasetIdentifier: {datasetIdentifier}, versionNumber: {versionNumber}:, type: {type}, filePath: {filePath}",
                  datasetIdentifier, versionNumber, type, filePath);

        try
        {
            return await fileService.Upload(datasetVersion, type, filePath, new(Request.Body, Request.Headers.ContentLength.Value));
        }
        catch (IllegalPathException)
        {
            return IllegalPathResult();
        }
        catch (ConflictException)
        {
            return ConflictResult();
        }
        catch (DatasetStatusException)
        {
            return StatusMismatchResult();
        }
    }


    [HttpDelete("file/{datasetIdentifier}/{versionNumber}/{type}")]
    [Authorize(Roles = "User")]
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
            await fileService.Delete(datasetVersion, type, filePath);
        }
        catch (IllegalPathException)
        {
            return IllegalPathResult();
        }
        catch (ConflictException)
        {
            return ConflictResult();
        }
        catch (DatasetStatusException)
        {
            return StatusMismatchResult();
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
            var fileData = await fileService.GetData(datasetVersion, type, filePath);

            if (fileData == null)
            {
                return NotFound();
            }

            Response.Headers.ContentLength = fileData.Length;

            return File(fileData.Stream, "application/octet-stream", filePath);
        }
        catch (IllegalPathException)
        {
            return IllegalPathResult();
        }
    }

    [HttpGet("/file/{datasetIdentifier}/{versionNumber}/zip")]
    public async Task GetDataAsZip(string datasetIdentifier, string versionNumber, [FromQuery]string[] path)
    {
        logger.LogDebug("GetDataAsZip datasetIdentifier: {datasetIdentifier}, versionNumber: {versionNumber}, path {path}",
            datasetIdentifier, versionNumber, path);

        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        Response.ContentType = "application/zip";
        Response.Headers.ContentDisposition = "attachment; filename=" + datasetIdentifier + "-" + versionNumber + ".zip";

        using var archive = new ZipArchive(Response.BodyWriter.AsStream(), ZipArchiveMode.Create, false);

        await foreach (var (type, filePath, data) in fileService.GetDataByPaths(datasetVersion, path))
        {
            var entry = archive.CreateEntry(type.ToString().ToLower() + '/' + filePath, CompressionLevel.NoCompression);
            using var entryStream = entry.Open();
            await data.Stream.CopyToAsync(entryStream);
        }

        /*await foreach (var file in fileService.ListFiles(datasetVersion))
        {
            string filePath = file.AdditionalType.ToString().ToLowerInvariant() + '/' + file.Id;

            if (path.Length > 0 && !path.Any(filePath.StartsWith))
            {
                continue;
            }

            var fileData = await fileService.GetData(datasetVersion, file.AdditionalType, file.Id);

            if (fileData != null)
            {
                var entry = archive.CreateEntry(filePath, CompressionLevel.NoCompression);
                using var entryStream = entry.Open();
                await fileData.Stream.CopyToAsync(entryStream);
            }
        }*/
    }

    [HttpGet("/file/{datasetIdentifier}/{versionNumber}")]
    [Authorize(Roles = "UploadService")]
    public async IAsyncEnumerable<RoCrateFile> ListFiles(string datasetIdentifier, string versionNumber)
    {
        logger.LogDebug("ListFiles datasetIdentifier: {datasetIdentifier}, versionNumber: {versionNumber}",
            datasetIdentifier, versionNumber);

        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        await foreach (var file in fileService.ListFiles(datasetVersion))
        {
            yield return file;
        }
    }

    private bool CheckClaims(DatasetVersionIdentifier datasetVersion) =>
        HttpContext.User.Identity is ClaimsIdentity identity &&
        identity.FindFirst("DatasetIdentifier")?.Value == datasetVersion.DatasetIdentifier &&
        identity.FindFirst("VersionNumber")?.Value == datasetVersion.VersionNumber;

    private ObjectResult IllegalPathResult() =>
        Problem("Illegal path.", statusCode: 400);

    private ObjectResult ConflictResult() =>
        Problem("Write conflict.", statusCode: 409);

    private ObjectResult StatusMismatchResult() =>
        Problem("Status mismatch.", statusCode: 400);

    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("/error")]
    public IActionResult HandleError() =>
        Problem();
}
