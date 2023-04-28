using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using DatasetFileUpload.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using System.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
namespace DatasetFileUpload.Controllers;

public class AuthController : Controller
{
    private readonly ILogger logger;
    private readonly IConfiguration configuration;
    private TokenService tokenService;

    public AuthController(ILogger<AuthController> logger, IConfiguration configuration)
    {
        this.logger = logger;
        this.configuration = configuration;
        this.tokenService = new TokenService(configuration);
    }

    [HttpPost("token/{datasetIdentifier}/{versionNumber}"), Authorize(Roles = "UploadService", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<string> GetUploadToken(string datasetIdentifier, string versionNumber, [FromBody] AuthInfo user)
    {
        return tokenService.GetUploadToken(user, datasetIdentifier, versionNumber);
    }

}
