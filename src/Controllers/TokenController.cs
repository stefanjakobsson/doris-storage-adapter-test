using DorisStorageAdapter.Configuration;
using DorisStorageAdapter.Controllers.Attributes;
using DorisStorageAdapter.Controllers.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NetDevPack.Security.Jwt.Core.Interfaces;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Controllers;

[DevOnly]
[ApiExplorerSettings(IgnoreApi = true)]
[ApiController]
public class TokenController(IJwtService jwtService, IOptions<GeneralConfiguration> configuration) : Controller
{
    private readonly IJwtService jwtService = jwtService;
    private readonly GeneralConfiguration configuration = configuration.Value;

    [HttpPost("dev/token/{datasetIdentifier}/{versionNumber}")]
    public Task<string> CreateDataAccessToken(string datasetIdentifier, string versionNumber, [FromQuery] string role)
    {
        return CreateToken(datasetIdentifier, versionNumber, role);
    }

    private async Task<string> CreateToken(string datasetIdentifier, string versionNumber, string role)
    {
        var key = await jwtService.GetCurrentSigningCredentials();

        var tokenHandler = new JsonWebTokenHandler();
        return tokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = configuration.PublicUrl.Scheme + "://" + configuration.PublicUrl.Authority,
            Audience = configuration.PublicUrl.ToString(),
            Subject = new([
                    new Claim("role", role),
                    new Claim(Claims.DatasetIdentifier, datasetIdentifier),
                    new Claim(Claims.DatasetVersionNumber, versionNumber)
                 ]),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = key
        });
    }
}
