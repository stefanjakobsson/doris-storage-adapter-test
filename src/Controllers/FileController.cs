using System.IO;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using DatasetFileUpload.Models;
using Microsoft.Extensions.Logging;

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
    public IActionResult Upload(string datasetIdentifier, string versionNumber, FileType type, AddFileRequest request)
    {

        // check if user is authenticated 

        // check if user exist in manifest

        // check if current dataset version is not published (publicationDate is set in manifest)

        // if type === data check if manifest conditionsOfAccess is PUBLIC (file of type data needs to generate url)

        logger.LogInformation($"Upload POST upload datasetIdentifier: {datasetIdentifier}, versionNumber: {versionNumber}:, FileName: {request.File.FileName}");
        
        // send file to storage service


        // Base path for file based on dataset version and type of file
        string folderPath = datasetIdentifier + "/" + datasetIdentifier + "-" + versionNumber + "/" + type.ToString().ToLower();
        
        // Append subfolder path if set
        if(request.Folder != null && request.Folder.Length > 0){
            folderPath += "/" + request.Folder;
        }
        
        logger.LogInformation($"Store file {request.File.FileName} in \"{folderPath}/{request.File.FileName}\"");

        /* RETURN:
        {
            "@type": "File",
            "@id": "data/data.csv",
            "contentSize": 4242,
            "sha256": "XXXXXXXXXXXXXXXXXXXXX",
            "dateCreated": "2022-02-21T11:45:20Z",
            "dateModified": "2022-02-22T15:50:30Z",
            "encodingFormat": "text/csv",
            "url": "https://example.org/record/04679b46-964c-11ec-b909-0242ac120002/data.csv"
        }
        */

        logger.LogInformation(request.File.ToString());

        //Models.File file = new Models.File(folderPath + '/' + request.File.FileName, request.File.Length);

        return Ok(new
        {
            Success = true
        });
    }


    [HttpDelete("file/{datasetIdentifier}/{versionNumber}/{type}")]
    public IActionResult Delete(string datasetIdentifier, string versionNumber, FileType type, string filePath)
    {
        // TODO: do some validation of filePath
        return Ok(new
        {
            Success = true
        });
    }

}
