using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using DatasetFileUpload.Models;
using Microsoft.Extensions.Logging;
using DatasetFileUpload.Services.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.WebUtilities;
using DatasetFileUpload.Controllers.Filters;

namespace DatasetFileUpload.Controllers;

[ApiController]
[Authorize]
public class FileController : Controller
{
    private readonly ILogger logger;
    private readonly IConfiguration configuration;
    private readonly IStorageService storageService;

    public FileController(ILogger<FileController> logger, IConfiguration configuration, IStorageService storageService)
    {
        this.logger = logger;
        this.configuration = configuration;
        this.storageService = storageService;
    }

    private bool CheckClaims(string datasetIdentifier, string versionNumber) =>
        HttpContext.User.Identity is ClaimsIdentity identity &&
        identity.FindFirst("DatasetIdentifier")?.Value == datasetIdentifier &&
        identity.FindFirst("VersionNumber")?.Value == versionNumber;

    [HttpPost("file/{datasetIdentifier}/{versionNumber}/{type}")]
    [Authorize(Roles = "User", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    // Disable form value model binding to ensure that files are not buffered
    [DisableFormValueModelBinding]
    // Disable request size limit to allow streaming large files
    [DisableRequestSizeLimit]
    public async Task<ActionResult<IEnumerable<RoCrateFile>>> Upload(string datasetIdentifier, string versionNumber, UploadType type)
    {
        if (!CheckClaims(datasetIdentifier, versionNumber))
        {
            return Forbid();
        }

        // check if current dataset version is not published (publicationDate is set in manifest)

        // if type === data check if manifest conditionsOfAccess is PUBLIC (file of type data needs to generate url)

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

        var roCrateFiles = new List<RoCrateFile>();

        while (section != null)
        {
            if (ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition) &&
                contentDisposition.DispositionType.Equals("form-data") &&
                !string.IsNullOrEmpty(contentDisposition.FileName.Value))
            {
                string fileName = contentDisposition.FileName.Value;

                logger.LogInformation($"Upload POST upload datasetIdentifier: {datasetIdentifier}, versionNumber: {versionNumber}:, FileName: {fileName}");

                // send file to storage service

                roCrateFiles.Add(await storageService.StoreFile(datasetIdentifier, versionNumber, type, fileName, section.Body, true));

                // Base path for file based on dataset version and type of file
                string folderPath = datasetIdentifier + "/" + datasetIdentifier + "-" + versionNumber + "/" + type.ToString().ToLower();

                logger.LogInformation($"Store file {fileName} in \"{folderPath}/{fileName}\"");
            }

            section = await reader.ReadNextSectionAsync();
        }

        return roCrateFiles;
    }


    [HttpDelete("file/{datasetIdentifier}/{versionNumber}/{type}")]
    public async Task<IActionResult> Delete(string datasetIdentifier, string versionNumber, UploadType type, string filePath)
    {
        if (!CheckClaims(datasetIdentifier, versionNumber))
        {
            return Forbid();
        }

        await storageService.DeleteFile(datasetIdentifier, versionNumber, type, filePath);

        return Ok();
    }

}
