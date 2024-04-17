using DatasetFileUpload.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using NetDevPack.Security.Jwt.Core.Interfaces;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DatasetFileUpload.Controllers;

[ApiController]
public class TokenController(IJwtService jwtService) : Controller
{
    private readonly IJwtService jwtService = jwtService;

    [DevOnly]
    [HttpPost("dev/token/{datasetIdentifier}/{versionNumber}")]
    public async Task<string> GetDataAccessToken(string datasetIdentifier, string versionNumber, [FromQuery]string[] roles)
    {
        var key = await jwtService.GetCurrentSigningCredentials();

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = "https://localhost:7065",
                Audience = "upload",
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
