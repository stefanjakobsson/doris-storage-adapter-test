using DatasetFileUpload.Models;
using DatasetFileUpload.Services.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace DatasetFileUpload.Controllers;

[ApiController]
[Authorize]
public class AuthController(ILogger<AuthController> logger, IConfiguration configuration) : Controller
{
    private readonly ILogger logger = logger;
    private readonly IConfiguration configuration = configuration;
    private TokenService tokenService = new TokenService(configuration);

    [HttpPost("token/{datasetIdentifier}/{versionNumber}")]
    [Authorize(Roles = "UploadService", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public string GetUploadToken(string datasetIdentifier, string versionNumber, [FromBody] AuthInfo user)
    {
        return tokenService.GetUploadToken(user, new DatasetVersionIdentifier(datasetIdentifier, versionNumber));
    }
}
