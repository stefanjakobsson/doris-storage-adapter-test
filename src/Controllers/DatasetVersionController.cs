using DatasetFileUpload.Authorization;
using DatasetFileUpload.Models;
using DatasetFileUpload.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DatasetFileUpload.Controllers;

public class DatasetVersionController(FileService fileService) : Controller
{
    private readonly FileService fileService = fileService;

    [HttpPut("{datasetIdentifier}/{versionNumber}")]
    [Authorize(Roles = Roles.Service)]
    public async Task<OkResult> SetupDatasetVersion(string datasetIdentifier, string versionNumber)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        await fileService.SetupDatasetVersion(datasetVersion);

        return Ok();
    }

    [HttpPut("{datasetIdentifier}/{versionNumber}/publish")]
    [Authorize(Roles = Roles.Service)]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<OkResult> PublishDatasetVersion(
        string datasetIdentifier,
        string versionNumber,
        [FromForm] AccessRightEnum access_right,
        [FromForm] string doi)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        await fileService.PublishDatasetVersion(datasetVersion, access_right, doi);

        return Ok();
    }

    [HttpPut("{datasetIdentifier}/{versionNumber}/withdraw")]
    [Authorize(Roles = Roles.Service)]
    public async Task<OkResult> WithdrawDatasetVersion(string datasetIdentifier, string versionNumber)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        await fileService.WithdrawDatasetVersion(datasetVersion);

        return Ok();
    }
}
