using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using DatasetFileUpload.Models;
using Microsoft.Extensions.Logging;
using DatasetFileUpload.Services.Storage;

namespace DatasetFileUpload.Controllers;

public class FileController : Controller
{
    private readonly ILogger logger;
    private readonly IConfiguration configuration;

    public FileController(ILogger<FileController> logger, IConfiguration configuration)
    {
        this.logger = logger;
        this.configuration = configuration;
    }


    [HttpPost("file/{datasetIdentifier}/{versionNumber}/{type}")]
    public async Task<IEnumerable<RoCrateFile>> Upload(string datasetIdentifier, string versionNumber, UploadType type, IFormFileCollection files)
    {

        // check if user is authenticated 

        // check if user exist in manifest

        // check if current dataset version is not published (publicationDate is set in manifest)

        // if type === data check if manifest conditionsOfAccess is PUBLIC (file of type data needs to generate url)

        List<RoCrateFile> roCrateFiles = new List<RoCrateFile>();

        DiskStorageService diskStorageService = new DiskStorageService();

        foreach (IFormFile file in files){
            logger.LogInformation($"Upload POST upload datasetIdentifier: {datasetIdentifier}, versionNumber: {versionNumber}:, FileName: {file.FileName}");
            
            // send file to storage service

            roCrateFiles.Add(await diskStorageService.StoreFile(datasetIdentifier, versionNumber, type, file, true));

            // Base path for file based on dataset version and type of file
            string folderPath = datasetIdentifier + "/" + datasetIdentifier + "-" + versionNumber + "/" + type.ToString().ToLower();
            
            logger.LogInformation($"Store file {file.FileName} in \"{folderPath}/{file.FileName}\"");
        }

        return  roCrateFiles;
    }


    [HttpDelete("file/{datasetIdentifier}/{versionNumber}/{type}")]
    public IActionResult Delete(string datasetIdentifier, string versionNumber, UploadType type, string filePath)
    {
        // TODO: do some validation of filePath
        return Ok(new
        {
            Success = true
        });
    }

}
