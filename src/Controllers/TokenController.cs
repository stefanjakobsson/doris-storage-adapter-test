using DatasetFileUpload.Authorization;
using DatasetFileUpload.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NetDevPack.Security.Jwt.Core.Interfaces;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DatasetFileUpload.Controllers;

[ApiController]
public class TokenController(IJwtService jwtService, IOptions<GeneralConfiguration> configuration) : Controller
{
    private readonly IJwtService jwtService = jwtService;
    private readonly GeneralConfiguration configuration = configuration.Value;

    [DevOnly]
    [HttpPost("dev/token/{datasetIdentifier}/{versionNumber}")]
    [Authorize(Roles = Roles.Service)]
    public async Task<string> GetDataAccessToken(string datasetIdentifier, string versionNumber, [FromQuery]string[] roles)
    {
        var key = await jwtService.GetCurrentSigningCredentials();

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = configuration.PublicUrl,
                Audience = configuration.PublicUrl,
                Subject = new([
                    ..roles.Select(r => new Claim("role", r)),
                    new Claim(Claims.DatasetIdentifier, datasetIdentifier),
                    new Claim(Claims.DatasetVersionNumber, versionNumber)
                ]),
                Expires = DateTime.UtcNow.AddHours(12),
                SigningCredentials = key
            });

            return tokenHandler.WriteToken(token);
    }
}
