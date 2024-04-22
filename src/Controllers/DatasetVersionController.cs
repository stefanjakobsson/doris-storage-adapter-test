using DatasetFileUpload.Authorization;
using DatasetFileUpload.Models;
using DatasetFileUpload.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DatasetFileUpload.Controllers;

public class DatasetVersionController(FileService fileService) : Controller
{
    private readonly FileService fileService = fileService;

    [HttpPut("{datasetIdentifier}/{versionNumber}")]
    [Authorize(Roles = Roles.Service)]
    public async Task<Results<Ok, ProblemHttpResult>> SetupVersion(string datasetIdentifier, string versionNumber)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        await fileService.SetupVersion(datasetVersion);

        return TypedResults.Ok();
    }

    [HttpPut("{datasetIdentifier}/{versionNumber}/publish")]
    [Authorize(Roles = Roles.Service)]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<Results<Ok, ProblemHttpResult>> PublishVersion(
        string datasetIdentifier,
        string versionNumber,
        [FromForm] AccessRightEnum access_right,
        [FromForm] string doi)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        await fileService.PublishVersion(datasetVersion, access_right, doi);

        return TypedResults.Ok();
    }

    [HttpPut("{datasetIdentifier}/{versionNumber}/withdraw")]
    [Authorize(Roles = Roles.Service)]
    public async Task<Results<Ok, ProblemHttpResult>> WithdrawVersion(string datasetIdentifier, string versionNumber)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        await fileService.WithdrawVersion(datasetVersion);

        return TypedResults.Ok();
    }
}
