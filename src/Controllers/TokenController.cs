using DorisStorageAdapter.Configuration;
using DorisStorageAdapter.Controllers.Attributes;
using DorisStorageAdapter.Controllers.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NetDevPack.Security.Jwt.Core.Interfaces;
using System;
using System.Collections.Generic;
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

    [HttpPost("dev/token")]
    public Task<string> CreateServiceToken()
    {
        return CreateToken(Roles.Service, []);
    }

    [HttpPost("dev/token/{datasetIdentifier}/{versionNumber}")]
    public Task<string> CreateDataAccessToken(string datasetIdentifier, string versionNumber, [FromQuery] bool write)
    {
        return CreateToken(write ? Roles.WriteData : Roles.ReadData, 
            [new Claim(Claims.DatasetIdentifier, datasetIdentifier),
            new Claim(Claims.DatasetVersionNumber, versionNumber)]);
    }

    private async Task<string> CreateToken(string role, IEnumerable<Claim> claims)
    {
        var key = await jwtService.GetCurrentSigningCredentials();

        var tokenHandler = new JsonWebTokenHandler();
        return tokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = configuration.PublicUrl,
            Audience = configuration.PublicUrl,
            Subject = new([
                    new Claim("role", role),
                    ..claims
                 ]),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = key
        });
    }
}
