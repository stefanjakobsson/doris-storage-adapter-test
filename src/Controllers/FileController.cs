using DorisStorageAdapter.Controllers.Attributes;
using DorisStorageAdapter.Controllers.Authorization;
using DorisStorageAdapter.Models;
using DorisStorageAdapter.Services;
using DorisStorageAdapter.Services.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Controllers;

[ApiController]
public class FileController(
    IFileService fileService,
    IAuthorizationService authorizationService,
    IAuthorizationPolicyProvider authorizationPolicyProvider) : ControllerBase
{
    private readonly IFileService fileService = fileService;
    private readonly IAuthorizationService authorizationService = authorizationService;
    private readonly IAuthorizationPolicyProvider authorizationPolicyProvider = authorizationPolicyProvider;

    [HttpPut("file/{identifier}/{version}/{type}")]
    [Authorize(Roles = Roles.WriteData)]
    [DisableRequestSizeLimit] // Disable request size limit to allow streaming large files
    // DisableFormValueModelBinding makes sure that ASP.NET does not try to parse the body as form data
    // when Content-Type is "multipart/form-data" or "application/x-www-form-urlencoded".
    [DisableFormValueModelBinding] 
    [EnableCors(nameof(StoreFile))]
    [BinaryRequestBody("*/*")]
    [ProducesResponseType<File>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status411LengthRequired, MediaTypeNames.Application.ProblemJson)]
    public async Task<Results<Ok<File>, ForbidHttpResult, ProblemHttpResult>> StoreFile(
        string identifier,
        string version,
        FileType type,
        [FromQuery, BindRequired] string filePath,
        CancellationToken cancellationToken)
    {
        var datasetVersion = new DatasetVersion(identifier, version);

        if (!CheckClaims(datasetVersion))
        {
            return TypedResults.Forbid();
        }

        if (Request.Headers.ContentLength == null)
        {
            return TypedResults.Problem("Missing Content-Length.", statusCode: 411);
        }

        var result = await fileService.StoreFile(
            datasetVersion: datasetVersion, 
            type: type, 
            filePath: filePath, 
            data: new(
                Stream: Request.Body, 
                Length: Request.Headers.ContentLength.Value), 
            contentType: Request.Headers.ContentType,
            cancellationToken: cancellationToken);

        return TypedResults.Ok(result);
    }

    [HttpDelete("file/{identifier}/{version}/{type}")]
    [Authorize(Roles = Roles.WriteData)]
    [EnableCors(nameof(DeleteFile))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, MediaTypeNames.Application.ProblemJson)]
    public async Task<Results<Ok, ForbidHttpResult>> DeleteFile(
        string identifier,
        string version,
        FileType type,
        [FromQuery, BindRequired] string filePath,
        CancellationToken cancellationToken)
    {
        var datasetVersion = new DatasetVersion(identifier, version);

        if (!CheckClaims(datasetVersion))
        {
            return TypedResults.Forbid();
        }

        await fileService.DeleteFile(datasetVersion, type, filePath, cancellationToken);

        return TypedResults.Ok();
    }

    [HttpPut("file/{identifier}/{version}/{type}/import")]
    [Authorize(Roles = Roles.Service)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<Results<Ok, ForbidHttpResult>> ImportFiles(
        string identifier,
        string version,
        FileType type,
        [FromQuery, BindRequired] string fromVersion,
        CancellationToken cancellationToken)
    {
        var datasetVersion = new DatasetVersion(identifier, version);

        if (!CheckClaims(datasetVersion))
        {
            return TypedResults.Forbid();
        }

        await fileService.ImportFiles(datasetVersion, type, fromVersion, cancellationToken);

        return TypedResults.Ok();
    }

    [HttpHead("file/{identifier}/{version}/{type}")]
    [HttpGet("file/{identifier}/{version}/{type}")]
    [SwaggerResponse(StatusCodes.Status200OK, null, typeof(FileStreamResult), "*/*")]
    [SwaggerResponse(StatusCodes.Status206PartialContent, null, typeof(FileStreamResult), "*/*")]
    [ProducesResponseType(typeof(void), StatusCodes.Status416RangeNotSatisfiable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<Results<FileStreamHttpResult, ForbidHttpResult, NotFound>> GetFileData(
        string identifier,
        string version,
        FileType type,
        [FromQuery, BindRequired] string filePath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        ByteRange? ParseByteRange()
        {
            var rangeHeader = Request.GetTypedHeaders().Range;
            if (rangeHeader != null && rangeHeader.Ranges.Count == 1)
            {
                var rangeItem = rangeHeader.Ranges.First();
                return new(rangeItem.From, rangeItem.To);
            }

            return null;
        }

        var datasetVersion = new DatasetVersion(identifier, version);
        bool restrictToPubliclyAccessible = true;

        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            var defaultPolicy = await authorizationPolicyProvider.GetDefaultPolicyAsync();
            var authorizationResult = await authorizationService.AuthorizeAsync(User, defaultPolicy);

            if (!authorizationResult.Succeeded ||
                !User.IsInRole(Roles.ReadData) ||
                !CheckClaims(datasetVersion))
            {
                return TypedResults.Forbid();
            }

            restrictToPubliclyAccessible = false;
        }

        var fileData = await fileService.GetFileData(
            datasetVersion: datasetVersion,
            type: type,
            filePath: filePath,
            isHeadRequest: Request.Method == HttpMethods.Head,
            byteRange: ParseByteRange(),
            restrictToPubliclyAccessible: restrictToPubliclyAccessible,
            cancellationToken: cancellationToken); 

        if (fileData == null)
        {
            return TypedResults.NotFound();
        }

        // Use a fake seekable stream here in order for TypedResults.Stream()
        // to work as intended when using byte ranges.
        // fileData.Data.Stream as returned from fileService.GetFileData() is already sliced
        // according to the given byte range, but the internal logic in TypedResults.Stream()
        // will try to seek according to the byte range. Using a FakeSeekableStream fixes
        // that by making seeking a no-op.
        fileData = fileData with
        {
            Data = new(
               Stream: new FakeSeekableStream(fileData.Data.Stream, fileData.Length),
               Length: fileData.Data.Length)
        };

        return TypedResults.Stream(
            stream: fileData.Data.Stream, 
            contentType: fileData.ContentType, 
            fileDownloadName: filePath.Split('/').Last(), 
            enableRangeProcessing: true);
    }

    [HttpGet("file/{identifier}/{version}/zip")]
    [Authorize(Roles = Roles.ReadData)]
    [SwaggerResponse(StatusCodes.Status200OK, null, typeof(FileStreamResult), MediaTypeNames.Application.Zip)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public Results<PushStreamHttpResult, ForbidHttpResult> GetFileDataAsZip(
        string identifier,
        string version,
        [FromQuery] string[] path,
        CancellationToken cancellationToken)
    {
        var datasetVersion = new DatasetVersion(identifier, version);

        if (!CheckClaims(datasetVersion))
        {
            return TypedResults.Forbid();
        }

        return TypedResults.Stream(_ => 
            fileService.WriteFileDataAsZip(
                datasetVersion, 
                path, 
                Response.BodyWriter.AsStream(),
                cancellationToken),
            MediaTypeNames.Application.Zip, 
            identifier + '-' + version + ".zip");
    }

    [HttpGet("file/{identifier}/{version}")]
    [Authorize(Roles = Roles.Service)]
    [ProducesResponseType<IEnumerable<File>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public Results<Ok<IAsyncEnumerable<File>>, ForbidHttpResult> ListFiles(
        string identifier, 
        string version,
        CancellationToken cancellationToken)
    {
        var datasetVersion = new DatasetVersion(identifier, version);

        if (!CheckClaims(datasetVersion))
        {
            return TypedResults.Forbid();
        }

        return TypedResults.Ok(fileService.ListFiles(datasetVersion, cancellationToken));
    }

    private bool CheckClaims(DatasetVersion datasetVersion) =>
        Claims.CheckClaims(datasetVersion, User.Claims);
}
