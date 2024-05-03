using DatasetFileUpload.Authorization;
using DatasetFileUpload.Models;
using DatasetFileUpload.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
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

        var result = await appService.StoreFile(
            datasetVersion, type, filePath, new(
                Stream: Request.Body, 
                Length: Request.Headers.ContentLength.Value, 
                ContentType: Request.Headers.ContentType));

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

        return TypedResults.Stream(fileData.Stream, fileData.ContentType, filePath.Split('/').Last());
    }

    [HttpGet("file/{datasetIdentifier}/{versionNumber}/zip")]
    [Authorize(Roles = Roles.ReadData)]
    [EnableCors(nameof(GetFileDataAsZip))]
    public Results<PushStreamHttpResult, ForbidHttpResult> GetFileDataAsZip(
        string datasetIdentifier,
        string versionNumber,
        [FromQuery] string[] path)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        if (!CheckDatasetVersionClaims(datasetVersion))
        {
            return TypedResults.Forbid();
        }

        return TypedResults.Stream(_ => 
            appService.WriteFileDataAsZip(datasetVersion, path, Response.BodyWriter.AsStream()), 
            "application/zip", datasetIdentifier + "-" + versionNumber + ".zip");
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
