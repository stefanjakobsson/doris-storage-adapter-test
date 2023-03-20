using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using DatasetFileUpload.Models;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;

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

    [HttpGet("token/{datasetIdentifier}/{versionNumber}")]
    public async Task<string> GetUploadToken(string datasetIdentifier, string versionNumber)
    {
        return CreateUploadToken(datasetIdentifier, versionNumber);
    }

    private string CreateUploadToken(string datasetIdentifier, string versionNumber)
    {
        List<Claim> claims = new List<Claim> {
            new Claim(ClaimTypes.Role, "User"),
            new Claim("DatasetIdentifier", datasetIdentifier),
            new Claim("VersionNumber", versionNumber)
        };

        var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration.GetSection("AppSettings:SigningKey").Value!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.Now.AddHours(12),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

}
