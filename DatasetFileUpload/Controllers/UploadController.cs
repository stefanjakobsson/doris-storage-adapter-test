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

    [HttpPost("upload/{datasetVersionIdentifier}")]
    public IActionResult AddFile(string datasetVersionIdentifier, AddFileRequest request)
    {

        logger.LogInformation($"Upload (POST upload datasetVersionIdentifier: {datasetVersionIdentifier}), file: {request.File.FileName}");
        logger.LogInformation($"Store file {request.File.FileName} to {datasetVersionIdentifier}/data/{request.Folder}/{request.File.FileName}");

        return Ok(new
        {
            Success = true
        });
    }

}
