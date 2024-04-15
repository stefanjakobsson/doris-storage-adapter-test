using DatasetFileUpload.Authorization;
using DatasetFileUpload.Models;
using DatasetFileUpload.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DatasetFileUpload.Controllers;

[ApiController]
public class TokenController(ILogger<TokenController> logger, IConfiguration configuration) : Controller
{
    private readonly ILogger logger = logger;
    private readonly IConfiguration configuration = configuration;
    private readonly TokenService tokenService = new(configuration);

    public record TokenRequest(
        string grant_type,
        string client_assertion_type,
        string client_assertion);

    [HttpPost("token")]
    [Consumes("application/x-www-form-urlencoded")]
    public IActionResult RequestServiceToken([FromForm] TokenRequest request)
    {
        return NoContent();
    }


    [HttpPost("token/{datasetIdentifier}/{versionNumber}")]
    [Authorize(Scope.readData)]
    public string GetDataToken(string datasetIdentifier, string versionNumber, AuthInfo user)
    {
        return "";
        //return tokenService.GetUserToken(user, new DatasetVersionIdentifier(datasetIdentifier, versionNumber));
    }
}
