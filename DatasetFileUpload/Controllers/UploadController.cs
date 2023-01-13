using System.IO;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using DatasetFileUpload.Models;
using Microsoft.Extensions.Logging;

namespace DatasetFileUpload.Controllers;

public class UploadController : Controller
{
    private readonly ILogger logger;
    private readonly IConfiguration configuration;

    public UploadController(ILogger<UploadController> logger, IConfiguration configuration)
    {
        this.logger = logger;
        this.configuration = configuration;
    }

    [HttpPost("upload/{datasetIdentifier}/{versionNumber}")]
    public IActionResult AddFile(string datasetIdentifier, string versionNumber, AddFileRequest request)
    {

        logger.LogInformation($"Upload (POST upload datasetIdentifier: {datasetIdentifier}), file: {request.File.FileName}");
        logger.LogInformation($"Store file {request.File.FileName} to {datasetIdentifier}/data/{request.Folder}/{request.File.FileName}");

        return Ok(new
        {
            Success = true
        });
    }

}
