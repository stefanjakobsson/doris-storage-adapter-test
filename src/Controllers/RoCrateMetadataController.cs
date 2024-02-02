using DatasetFileUpload.Services.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DatasetFileUpload.Controllers;

[ApiController]
[Authorize]
public class RoCrateMetadataController(ILogger<RoCrateMetadataController> logger, IStorageService storageService) : Controller
{
    private readonly ILogger logger = logger;
    private readonly IStorageService storageService = storageService;

    [HttpPost("/metadata/{datasetIdentifier}/{versionNumber}")]
    [Authorize(Roles = "UploadService", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public void CreateOrUpdateRoCrateMetadata(string datasetIdentifier, string versionNumber, JsonDocument metadata)
    {
        logger.LogInformation("Update RO-Crate metadata datasetIdentifier: {datasetIdentifier} versionNumber: {versionNumber})",
            datasetIdentifier, versionNumber);

        //<await storageService.StoreRoCrateMetadata(datasetIdentifier, versionNumber, metadata.RootElement.GetRawText());      
    }
}
