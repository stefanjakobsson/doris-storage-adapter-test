using System.IO;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;


namespace DatasetFileUpload.Controllers;

public class ManifestController : Controller
{
    private readonly ILogger logger;
    private readonly IConfiguration configuration;

    public ManifestController(ILogger<ManifestController> logger, IConfiguration configuration)
    {
        this.logger = logger;
        this.configuration = configuration;
    }

    [HttpPost("/manifest/{datasetIdentifier}/{versionNumber}/manifest"), Authorize(Roles = "UploadService", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public IActionResult CreateOrUpdateManifest(string datasetIdentifier, string versionNumber, JsonDocument manifest)
    {
        logger.LogInformation($"Manifest (POST manifest datasetVersionIdentifier: {datasetIdentifier} version: {versionNumber}) ");

        //TODO: store the updated manifest

        return Ok(new
        {
            Success = true
        });
    }

    [HttpGet("/manifest/{datasetIdentifier}/{versionNumber}/files"), , Authorize(Roles = "UploadService", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public IActionResult GetFiles(string datasetIdentifier, string versionNumber)
    {

        return Ok(); //TODO: return manifest
    }

}
