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

namespace DatasetFileUpload.Controllers;

[ApiController]
[Authorize]
public class FileController : Controller
{
    private readonly ILogger logger;
    private readonly IConfiguration configuration;

    private IStorageService storageService;

    public FileController(ILogger<FileController> logger, IConfiguration configuration)
    {
        this.logger = logger;
        this.configuration = configuration;
        this.storageService = new DiskStorageService();
    }


    [HttpPost("file/{datasetIdentifier}/{versionNumber}/{type}"), Authorize(Roles = "User", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IEnumerable<RoCrateFile>> Upload(string datasetIdentifier, string versionNumber, UploadType type, IFormFileCollection files)
    {

        // check if user is authenticated 

        // check if user exist in manifest

        // check if current dataset version is not published (publicationDate is set in manifest)

        // if type === data check if manifest conditionsOfAccess is PUBLIC (file of type data needs to generate url)

        List<RoCrateFile> roCrateFiles = new List<RoCrateFile>();

        foreach (IFormFile file in files){
            logger.LogInformation($"Upload POST upload datasetIdentifier: {datasetIdentifier}, versionNumber: {versionNumber}:, FileName: {file.FileName}");
            
            // send file to storage service

            roCrateFiles.Add(await storageService.StoreFile(datasetIdentifier, versionNumber, type, file, true));

            // Base path for file based on dataset version and type of file
            string folderPath = datasetIdentifier + "/" + datasetIdentifier + "-" + versionNumber + "/" + type.ToString().ToLower();
            
            logger.LogInformation($"Store file {file.FileName} in \"{folderPath}/{file.FileName}\"");
        }

        return  roCrateFiles;
    }


    [HttpDelete("file/{datasetIdentifier}/{versionNumber}/{type}")]
    public async void Delete(string datasetIdentifier, string versionNumber, UploadType type, string filePath)
    {
        await storageService.DeleteFile(datasetIdentifier, versionNumber, type, filePath);
    }

}
