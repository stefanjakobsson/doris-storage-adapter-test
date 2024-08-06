using DorisStorageAdapter.Authorization;
using DorisStorageAdapter.Models;
using DorisStorageAdapter.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Controllers;

[ApiController]
public class DatasetVersionController(ServiceImplementation appService) : Controller
{
    private readonly ServiceImplementation appService = appService;

    [HttpPut("{datasetIdentifier}/{versionNumber}")]
    [Authorize(Roles = Roles.Service)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, MediaTypeNames.Application.ProblemJson)]
    public async Task<OkResult> SetupDatasetVersion(string datasetIdentifier, string versionNumber)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        await appService.SetupDatasetVersion(datasetVersion);

        return Ok();
    }

    [HttpPut("{datasetIdentifier}/{versionNumber}/publish")]
    [Authorize(Roles = Roles.Service)]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, MediaTypeNames.Application.ProblemJson)]
    public async Task<OkResult> PublishDatasetVersion(
        string datasetIdentifier,
        string versionNumber,
        [FromForm] AccessRightEnum access_right,
        [FromForm] string doi)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        await appService.PublishDatasetVersion(datasetVersion, access_right, doi);

        return Ok();
    }

    [HttpPut("{datasetIdentifier}/{versionNumber}/withdraw")]
    [Authorize(Roles = Roles.Service)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, MediaTypeNames.Application.ProblemJson)]
    public async Task<OkResult> WithdrawDatasetVersion(string datasetIdentifier, string versionNumber)
    {
        var datasetVersion = new DatasetVersionIdentifier(datasetIdentifier, versionNumber);

        await appService.WithdrawDatasetVersion(datasetVersion);

        return Ok();
    }
}
