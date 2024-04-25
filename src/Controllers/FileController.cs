using DatasetFileUpload.Authorization;
using DatasetFileUpload.Models;
using DatasetFileUpload.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace DatasetFileUpload.Controllers;

[ApiController]
public class FileController(
    ServiceImplementation appService,
    IAuthorizationService authorizationService,
    IAuthorizationPolicyProvider authorizationPolicyProvider) : Controller
{
    private readonly ServiceImplementation appService = appService;
    private readonly IAuthorizationService authorizationService = authorizationService;
    private readonly IAuthorizationPolicyProvider authorizationPolicyProvider = authorizationPolicyProvider;

    // A possible drawback of not using POST with multipart/form-data is that 
    // using PUT requires CORS.
    [HttpPut("file/{datasetIdentifier}/{versionNumber}/{type}")]
    [Authorize(Roles = Roles.WriteData)]
    // Disable request size limit to allow streaming large files
    [DisableRequestSizeLimit]
    [EnableCors(nameof(StoreFile))]
    public async Task<Results<Ok<RoCrateFile>, ForbidHttpResult, ProblemHttpResult>> StoreFile(
        string datasetIdentifier,
        string versionNumber,
        FileTypeEnum type,
        [FromQuery] string filePath)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        if (!CheckDatasetVersionClaims(datasetVersion))
        {
            return TypedResults.Forbid();
        }

        if (Request.Headers.ContentLength == null)
        {
            return TypedResults.Problem("Missing Content-Length.", statusCode: 400);
        }

        var result = await appService.StoreFile(datasetVersion, type, filePath, new(Request.Body, Request.Headers.ContentLength.Value));
        return TypedResults.Ok(result);
    }


    [HttpDelete("file/{datasetIdentifier}/{versionNumber}/{type}")]
    [Authorize(Roles = Roles.WriteData)]
    [EnableCors(nameof(DeleteFile))]
    public async Task<Results<Ok, ForbidHttpResult>> DeleteFile(
        string datasetIdentifier,
        string versionNumber,
        FileTypeEnum type,
        string filePath)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        if (!CheckDatasetVersionClaims(datasetVersion))
        {
            return TypedResults.Forbid();
        }

        await appService.DeleteFile(datasetVersion, type, filePath);

        return TypedResults.Ok();
    }

    [HttpGet("file/{datasetIdentifier}/{versionNumber}/{type}")]
    [EnableCors(nameof(GetFileData))]
    public async Task<Results<FileStreamHttpResult, ForbidHttpResult, NotFound>> GetFileData(
        string datasetIdentifier,
        string versionNumber,
        FileTypeEnum type,
        string filePath)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);
        bool restrictToPubliclyAccessible = true;

        if (Request.Headers.Authorization.Count > 0)
        {
            var defaultPolicy = await authorizationPolicyProvider.GetDefaultPolicyAsync();
            var authorizationResult = await authorizationService.AuthorizeAsync(User, defaultPolicy);

            if (!authorizationResult.Succeeded ||
                !User.IsInRole(Roles.ReadData) ||
                !CheckDatasetVersionClaims(datasetVersion))
            {
                return TypedResults.Forbid();
            }

            restrictToPubliclyAccessible = false;
        }

        var fileData = await appService.GetFileData(datasetVersion, type, filePath, restrictToPubliclyAccessible);

        if (fileData == null)
        {
            return TypedResults.NotFound();
        }

        Response.Headers.ContentLength = fileData.Length;

        return TypedResults.Stream(fileData.Stream, "application/octet-stream", filePath);
    }

    [HttpGet("file/{datasetIdentifier}/{versionNumber}/zip")]
    [Authorize(Roles = Roles.ReadData)]
    [EnableCors(nameof(GetFileDataAsZip))]
    public async Task<Results<Ok, ForbidHttpResult>> GetFileDataAsZip(
        string datasetIdentifier,
        string versionNumber,
        [FromQuery] string[] path)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        if (!CheckDatasetVersionClaims(datasetVersion))
        {
            return TypedResults.Forbid();
        }

        Response.ContentType = "application/zip";
        Response.Headers.ContentDisposition = "attachment; filename=" + datasetIdentifier + "-" + versionNumber + ".zip";

        using var archive = new ZipArchive(Response.BodyWriter.AsStream(), ZipArchiveMode.Create, false);

        await foreach (var (type, filePath, data) in appService.GetFileDataByPaths(datasetVersion, path))
        {
            var entry = archive.CreateEntry(type.ToString() + '/' + filePath, CompressionLevel.NoCompression);
            using var entryStream = entry.Open();
            using var dataStream = data.Stream;
            await dataStream.CopyToAsync(entryStream);
        }

        return TypedResults.Ok();

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

    [HttpGet("file/{datasetIdentifier}/{versionNumber}")]
    [Authorize(Roles = Roles.Service)]
    public async IAsyncEnumerable<RoCrateFile> ListFiles(string datasetIdentifier, string versionNumber)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        await foreach (var file in appService.ListFiles(datasetVersion))
        {
            yield return file;
        }
    }

    private bool CheckDatasetVersionClaims(DatasetVersionIdentifier datasetVersion) =>
        User.Claims.Any(c => c.Type == Claims.DatasetIdentifier && c.Value == datasetVersion.DatasetIdentifier) &&
        User.Claims.Any(c => c.Type == Claims.DatasetVersionNumber && c.Value == datasetVersion.VersionNumber);
}
