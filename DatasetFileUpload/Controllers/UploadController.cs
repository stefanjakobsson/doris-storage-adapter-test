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

        logger.LogInformation($"Upload POST upload datasetIdentifier: {datasetIdentifier}, versionNumber: {versionNumber}:, FileName: {request.File.FileName}");
        
        // Base path for file based on dataset version and type of file
        string folderPath = datasetIdentifier + "/" + datasetIdentifier + "-" + versionNumber + "/" + request.Type.ToString().ToLower();
        
        // Append subfolder path if set
        if(request.Folder != null && request.Folder.Length > 0){
            folderPath += "/" + request.Folder;
        }
        
        logger.LogInformation($"Store file {request.File.FileName} in \"{folderPath}/{request.File.FileName}\"");

        return Ok(new
        {
            Success = true
        });
    }

}
