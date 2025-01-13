using DorisStorageAdapter.Controllers.Authorization;
using DorisStorageAdapter.Models;
using DorisStorageAdapter.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Controllers;

[ApiController]
public class DatasetVersionController(ServiceImplementation appService) : ControllerBase
{
    private readonly ServiceImplementation appService = appService;

    [HttpPut("{identifier}/{version}/publish")]
    [Authorize(Roles = Roles.Service)]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, MediaTypeNames.Application.ProblemJson)]
    public async Task<Results<Ok, ForbidHttpResult>> PublishDatasetVersion(
        string identifier,
        string version,
        [FromForm(Name = "access_right")] AccessRight accessRight,
        [FromForm] string doi,
        CancellationToken cancellationToken)
    {
        var datasetVersion = new DatasetVersion(identifier, version);

        if (!CheckDatasetVersionClaims(datasetVersion))
        {
            return TypedResults.Forbid();
        }

        await appService.PublishDatasetVersion(datasetVersion, accessRight, doi, cancellationToken);

        return TypedResults.Ok();
    }

    [HttpPut("{identifier}/{version}/withdraw")]
    [Authorize(Roles = Roles.Service)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, MediaTypeNames.Application.ProblemJson)]
    public async Task<Results<Ok, ForbidHttpResult>> WithdrawDatasetVersion(
        string identifier, 
        string version,
        CancellationToken cancellationToken)
    {
        var datasetVersion = new DatasetVersion(identifier, version);

        if (!CheckDatasetVersionClaims(datasetVersion))
        {
            return TypedResults.Forbid();
        }

        await appService.WithdrawDatasetVersion(datasetVersion, cancellationToken);

        return TypedResults.Ok();
    }

    private bool CheckDatasetVersionClaims(DatasetVersion datasetVersion) =>
       Claims.CheckClaims(datasetVersion, User.Claims);
}
