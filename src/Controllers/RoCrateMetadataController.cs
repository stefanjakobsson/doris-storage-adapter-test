namespace DatasetFileUpload.Controllers;

using DatasetFileUpload.Services.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading.Tasks;

[ApiController]
[Authorize]
public class RoCrateMetadataController : Controller
{
    private readonly ILogger logger;
    private readonly IStorageService storageService;

    public RoCrateMetadataController(ILogger<RoCrateMetadataController> logger, IStorageService storageService)
    {
        this.logger = logger;
        this.storageService = storageService;
    }

    [HttpPost("/metadata/{datasetIdentifier}/{versionNumber}")]
    [Authorize(Roles = "UploadService", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task CreateOrUpdateRoCrateMetadata(string datasetIdentifier, string versionNumber, JsonDocument metadata)
    {
        logger.LogInformation("Update RO-Crate metadata datasetIdentifier: {datasetIdentifier} versionNumber: {versionNumber})",
            datasetIdentifier, versionNumber);

        await storageService.StoreRoCrateMetadata(datasetIdentifier, versionNumber, metadata.RootElement.GetRawText());      
    }

    [HttpGet("/metadata/{datasetIdentifier}/{versionNumber}/files")]
    [Authorize(Roles = "UploadService", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public IActionResult GetFiles(string datasetIdentifier, string versionNumber)
    {

        return Ok(); //TODO: return manifest
    }

}
