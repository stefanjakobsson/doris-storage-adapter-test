using DorisStorageAdapter.Server.Configuration;
using DorisStorageAdapter.Server.Controllers.Attributes;
using DorisStorageAdapter.Server.Controllers.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NetDevPack.Security.Jwt.Core.Interfaces;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Server.Controllers;

[DevOnly]
[ApiExplorerSettings(IgnoreApi = true)]
[ApiController]
public sealed class TokenController(IJwtService jwtService, IOptions<GeneralConfiguration> configuration) : ControllerBase
{
    private readonly IJwtService jwtService = jwtService;
    private readonly GeneralConfiguration configuration = configuration.Value;

    [HttpPost("dev/token/{identifier}/{version}")]
    public Task<string> CreateDataAccessToken(string identifier, string version, [FromQuery] string role)
    {
        return CreateToken(identifier, version, role);
    }

    private async Task<string> CreateToken(string identifier, string version, string role)
    {
        var key = await jwtService.GetCurrentSigningCredentials();

        var tokenHandler = new JsonWebTokenHandler();
        return tokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = configuration.PublicUrl.Scheme + "://" + configuration.PublicUrl.Authority,
            Audience = configuration.PublicUrl.AbsoluteUri,
            Subject = new([
                    new Claim("role", role),
                    new Claim(Claims.DatasetIdentifier, identifier),
                    new Claim(Claims.DatasetVersion, version)
                 ]),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = key
        });
    }
}
