using System.IO;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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

    [HttpPost("/manifest/{datasetIdentifier}/{versionNumber}/manifest")]
    public IActionResult CreateOrUpdateManifest(string datasetIdentifier, string versionNumber, JsonDocument manifest)
    {
        logger.LogInformation($"Manifest (POST manifest datasetVersionIdentifier: {datasetIdentifier} version: {versionNumber}) ");

        return Ok(new
        {
            Success = true
        });
    }

    [HttpGet("/manifest/{datasetIdentifier}/{versionNumber}/files")]
    public IActionResult GetFiles(string datasetIdentifier, string versionNumber)
    {

        return Ok(); //TODO: return manifest
    }

}
